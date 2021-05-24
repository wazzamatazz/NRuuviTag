using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NRuuviTag.Mqtt {

    /// <summary>
    /// <see cref="JsonConverter{T}"/> for <see cref="RuuviTagSample"/>.
    /// </summary>
    /// <remarks>
    ///   This converter is used in order to remove properties such as 
    ///   <see cref="RuuviTagSample.MacAddress"/> from the serialized payload that is sent to an 
    ///   MQTT broker.
    /// </remarks>
    internal class RuuviTagSampleJsonConverter : JsonConverter<RuuviTagSample> {

        /// <summary>
        /// Gets the JSON property according to the naming policy of the specified 
        /// <see cref="JsonSerializerOptions"/>.
        /// </summary>
        /// <param name="name">
        ///   The property name.
        /// </param>
        /// <param name="options">
        ///   The JSON options.
        /// </param>
        /// <returns>
        ///   The property name according to the naming policy for the specified 
        ///   <paramref name="options"/>.
        /// </returns>
        private string GetPropertyName(string name, JsonSerializerOptions? options) {
            return options?.PropertyNamingPolicy?.ConvertName(name) ?? name;
        }


        /// <inheritdoc/>
        public override RuuviTagSample? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            // We don't read samples.
            throw new NotImplementedException();
        }


        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, RuuviTagSample value, JsonSerializerOptions options) {
            if (value == null) {
                writer.WriteNullValue();
                return;
            }
            
            writer.WriteStartObject();

            if (value.Timestamp != default) {
                writer.WritePropertyName(GetPropertyName(nameof(value.Timestamp), options));
                JsonSerializer.Serialize(writer, value.Timestamp.UtcDateTime, options);
            }

            if (value.SignalStrength != null) {
                writer.WritePropertyName(GetPropertyName(nameof(value.SignalStrength), options));
                JsonSerializer.Serialize(writer, value.SignalStrength, options);
            }

            if (value.Temperature != null) {
                writer.WritePropertyName(GetPropertyName(nameof(value.Temperature), options));
                JsonSerializer.Serialize(writer, value.Temperature, options);
            }

            if (value.Humidity != null) {
                writer.WritePropertyName(GetPropertyName(nameof(value.Humidity), options));
                JsonSerializer.Serialize(writer, value.Humidity, options);
            }

            if (value.Pressure != null) {
                writer.WritePropertyName(GetPropertyName(nameof(value.Pressure), options));
                JsonSerializer.Serialize(writer, value.Pressure, options);
            }

            if (value.AccelerationX != null) {
                writer.WritePropertyName(GetPropertyName(nameof(value.AccelerationX), options));
                JsonSerializer.Serialize(writer, value.AccelerationX, options);
            }

            if (value.AccelerationY != null) {
                writer.WritePropertyName(GetPropertyName(nameof(value.AccelerationY), options));
                JsonSerializer.Serialize(writer, value.AccelerationY, options);
            }

            if (value.AccelerationZ != null) {
                writer.WritePropertyName(GetPropertyName(nameof(value.AccelerationZ), options));
                JsonSerializer.Serialize(writer, value.AccelerationZ, options);
            }

            if (value.BatteryVoltage != null) {
                writer.WritePropertyName(GetPropertyName(nameof(value.BatteryVoltage), options));
                JsonSerializer.Serialize(writer, value.BatteryVoltage, options);
            }

            if (value.TxPower != null) {
                writer.WritePropertyName(GetPropertyName(nameof(value.TxPower), options));
                JsonSerializer.Serialize(writer, value.TxPower, options);
            }

            if (value.MovementCounter != null) {
                writer.WritePropertyName(GetPropertyName(nameof(value.MovementCounter), options));
                JsonSerializer.Serialize(writer, value.MovementCounter, options);
            }

            if (value.MeasurementSequence != null) {
                writer.WritePropertyName(GetPropertyName(nameof(value.MeasurementSequence), options));
                JsonSerializer.Serialize(writer, value.MeasurementSequence, options);
            }

            writer.WriteEndObject();
        }
    }
}
