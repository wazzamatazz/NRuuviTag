﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace NRuuviTag.Mqtt {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("NRuuviTag.Mqtt.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MQTT bridge is already running..
        /// </summary>
        internal static string Error_MqttBridgeIsAlreadyRunning {
            get {
                return ResourceManager.GetString("Error_MqttBridgeIsAlreadyRunning", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Stopped MQTT bridge..
        /// </summary>
        internal static string LogMessage_MqttBridgeStopped {
            get {
                return ResourceManager.GetString("LogMessage_MqttBridgeStopped", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Connected to MQTT broker..
        /// </summary>
        internal static string LogMessage_MqttClientConnected {
            get {
                return ResourceManager.GetString("LogMessage_MqttClientConnected", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Disconnected from MQTT broker..
        /// </summary>
        internal static string LogMessage_MqttClientDisconnected {
            get {
                return ResourceManager.GetString("LogMessage_MqttClientDisconnected", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MQTT publish error..
        /// </summary>
        internal static string LogMessage_MqttPublishError {
            get {
                return ResourceManager.GetString("LogMessage_MqttPublishError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Starting MQTT bridge..
        /// </summary>
        internal static string LogMessage_StartingMqttBridge {
            get {
                return ResourceManager.GetString("LogMessage_StartingMqttBridge", resourceCulture);
            }
        }
    }
}
