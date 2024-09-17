using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli {

    /// <summary>
    /// Implements both <see cref="ITypeRegistrar"/> and <see cref="ITypeResolver"/> for use in the 
    /// RuuviTag publisher <see cref="CommandApp"/>.
    /// </summary>
    public class TypeResolver : ITypeRegistrar, ITypeResolver {

        /// <summary>
        /// The generic <see cref="IEnumerable{T}"/> type.
        /// </summary>
        private static readonly Type s_ienumerableType = typeof(IEnumerable<>);

        /// <summary>
        /// Service registrations made by the <see cref="CommandApp"/>.
        /// </summary>
        private readonly ConcurrentDictionary<Type, List<ServiceRegistration>> _services = new ConcurrentDictionary<Type, List<ServiceRegistration>>();

        /// <summary>
        /// The <see cref="IServiceProvider"/> that is used to resolve services that have not been 
        /// registered directly with the <see cref="TypeResolver"/>.
        /// </summary>
        public IServiceProvider ServiceProvider { get; }


        /// <summary>
        /// Creates a new <see cref="TypeResolver"/> instance.
        /// </summary>
        /// <param name="serviceProvider">
        ///   The <see cref="IServiceProvider"/> that is used to resolve services that have not 
        ///   been registered directly with the <see cref="TypeResolver"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="serviceProvider"/> is <see langword="null"/>.
        /// </exception>
        public TypeResolver(IServiceProvider serviceProvider) {
            ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }


        /// <inheritdoc/>
        public ITypeResolver Build() {
            return this;
        }


        /// <inheritdoc/>
        public object? Resolve(Type? type) {
            if (type == null) {
                return null;
            }

            var resolveAll = type.IsGenericType && type.IsInterface && type.GetGenericTypeDefinition() == s_ienumerableType;
            var typeToResolve = resolveAll
                ? type.GetGenericArguments()[0] 
                : type;

            if (resolveAll) {
                var containerRegistrations = ServiceProvider.GetServices(typeToResolve);
                return _services.TryGetValue(typeToResolve, out var localRegistrations)
                    ? localRegistrations.Select(x => x.GetValue(ServiceProvider)).Concat(containerRegistrations).ToArray()
                    : containerRegistrations;
            }

            if (_services.TryGetValue(typeToResolve, out var registrations)) {
                return registrations.FirstOrDefault().GetValue(ServiceProvider);
            }

            return ServiceProvider.GetService(typeToResolve);
        }


        /// <inheritdoc/>
        public void Register(Type service, Type implementation) {
            _services.GetOrAdd(service, _ => new List<ServiceRegistration>()).Add(new ServiceRegistration(ImplementationType: implementation));
        }


        /// <inheritdoc/>
        public void RegisterInstance(Type service, object implementation) {
            _services.GetOrAdd(service, _ => new List<ServiceRegistration>()).Add(new ServiceRegistration(ImplementationInstance: implementation));
        }


        /// <inheritdoc/>
        public void RegisterLazy(Type service, Func<object> factory) {
            _services.GetOrAdd(service, _ => new List<ServiceRegistration>()).Add(new ServiceRegistration(ImplementationFactory: factory));
        }


        /// <summary>
        /// Represents a service registered directly with a <see cref="TypeResolver"/>.
        /// </summary>
        /// <param name="ImplementationType">
        ///   The implementation type.
        /// </param>
        /// <param name="ImplementationFactory">
        ///   The implementation factory.
        /// </param>
        /// <param name="ImplementationInstance">
        ///   The implementation instance.
        /// </param>
        private readonly record struct ServiceRegistration(Type? ImplementationType = null, Func<object>? ImplementationFactory = null, object? ImplementationInstance = null) {

            /// <summary>
            /// Gets the value from the service registration.
            /// </summary>
            /// <param name="serviceProvider">
            ///   The service provider to use to create the service instance when <see cref="ImplementationType"/> 
            ///   is configured.
            /// </param>
            /// <returns>
            ///   The service instance.
            /// </returns>
            public object? GetValue(IServiceProvider serviceProvider) {
                if (ImplementationType != null) {
                    return ActivatorUtilities.CreateInstance(serviceProvider, ImplementationType);
                }
                else if (ImplementationFactory != null) {
                    return ImplementationFactory();
                }
                else {
                    return ImplementationInstance;
                }
            }

        }

    }
}
