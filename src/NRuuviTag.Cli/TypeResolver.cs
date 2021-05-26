using System;
using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli {

    /// <summary>
    /// Implements both <see cref="ITypeRegistrar"/> and <see cref="ITypeResolver"/> for use in the 
    /// MQTT publisher <see cref="CommandApp"/>.
    /// </summary>
    public class TypeResolver : ITypeRegistrar, ITypeResolver {

        /// <summary>
        /// Service registrations made by the <see cref="CommandApp"/> that have a service type and 
        /// (optionally) an implementation type.
        /// </summary>
        private readonly ConcurrentDictionary<Type, Type?> _types = new ConcurrentDictionary<Type, Type?>();

        /// <summary>
        /// Service registrations made by the <see cref="CommandApp"/> that have a pre-initialised 
        /// instance.
        /// </summary>
        private readonly ConcurrentDictionary<Type, object> _implementations = new ConcurrentDictionary<Type, object>();

        /// <summary>
        /// Service registrations made by the <see cref="CommandApp"/> that provide a factory 
        /// function.
        /// </summary>
        private readonly ConcurrentDictionary<Type, Lazy<object>> _factories = new ConcurrentDictionary<Type, Lazy<object>>();


        /// <summary>
        /// The <see cref="IServiceProvider"/> that is used to resolve services that have not been 
        /// registered directly with the <see cref="TypeResolver"/>.
        /// </summary>
        public IServiceProvider? ServiceProvider { get; set; }


        /// <inheritdoc/>
        public ITypeResolver Build() {
            return this;
        }


        /// <inheritdoc/>
        public object? Resolve(Type? type) {
            if (type == null) {
                return null;
            }

            if (_implementations.TryGetValue(type, out var implementation)) {
                return implementation;
            }
            if (_factories.TryGetValue(type, out var factory)) {
                return factory.Value;
            }

            if (!_types.TryGetValue(type, out var implType) || implType == null) {
                implType = type;
            }

            if (ServiceProvider != null) {
                return ActivatorUtilities.CreateInstance(ServiceProvider, implType);
            }

            return Activator.CreateInstance(implType);
        }


        /// <inheritdoc/>
        public void Register(Type service, Type implementation) {
            _types[service] = implementation;
        }


        /// <inheritdoc/>
        public void RegisterInstance(Type service, object implementation) {
            _implementations[service] = implementation;
        }


        /// <inheritdoc/>
        public void RegisterLazy(Type service, Func<object> factory) {
            _factories[service] = new Lazy<object>(factory, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        }

    }
}
