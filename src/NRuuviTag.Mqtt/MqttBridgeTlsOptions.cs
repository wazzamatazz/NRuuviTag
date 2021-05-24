using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace NRuuviTag.Mqtt {

    /// <summary>
    /// TLS-related options for <see cref="MqttBridge"/>.
    /// </summary>
    public class MqttBridgeTlsOptions {

        /// <summary>
        /// Specifies if TLS should be used for the MQTT connection.
        /// </summary>
        public bool UseTls { get; set; } = true;

        /// <summary>
        /// The client certificates to use. Ignored if <see cref="UseTls"/> is <see langword="false"/>.
        /// </summary>
        public IEnumerable<X509Certificate>? ClientCertificates { get; set; }

        /// <summary>
        /// Specifies if untrusted server certificates will be allowed when validating server 
        /// certificates.
        /// </summary>
        public bool AllowUntrustedCertificates { get; set; }

        /// <summary>
        /// Specifies if certificate chain errors will be ignored when validating server 
        /// certificates.
        /// </summary>
        public bool IgnoreCertificateChainErrors { get; set; }

        /// <summary>
        /// A callback that can be used to perform custom validation of server certificates.
        /// </summary>
        /// <remarks>
        ///   Use this callback if you require certificate validation control beyond that offered 
        ///   by the <see cref="AllowUntrustedCertificates"/> and <see cref="IgnoreCertificateChainErrors"/> 
        ///   flags.
        /// </remarks>
        public Func<X509Certificate, X509Chain, SslPolicyErrors, bool>? ValidateServerCertificate { get; set; }

    }

}
