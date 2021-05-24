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

            writer.WritePropertyName("timestamp");
            JsonSerializer.Serialize(writer, value.Timestamp.UtcDateTime, options);

            writer.WritePropertyName("signalStrength");
            JsonSerializer.Serialize(writer, value.SignalStrength, options);

            if (value.Temperature != null) {
                writer.WritePropertyName("temperature");
                JsonSerializer.Serialize(writer, value.Temperature, options);
            }

            if (value.Humidity != null) {
                writer.WritePropertyName("humidity");
                JsonSerializer.Serialize(writer, value.Humidity, options);
            }

            if (value.Pressure != null) {
                writer.WritePropertyName("pressure");
                JsonSerializer.Serialize(writer, value.Pressure, options);
            }

            if (value.AccelerationX != null) {
                writer.WritePropertyName("accelerationX");
                JsonSerializer.Serialize(writer, value.AccelerationX, options);
            }

            if (value.AccelerationY != null) {
                writer.WritePropertyName("accelerationY");
                JsonSerializer.Serialize(writer, value.AccelerationY, options);
            }

            if (value.AccelerationZ != null) {
                writer.WritePropertyName("accelerationZ");
                JsonSerializer.Serialize(writer, value.AccelerationZ, options);
            }

            if (value.BatteryVoltage != null) {
                writer.WritePropertyName("batteryVoltage");
                JsonSerializer.Serialize(writer, value.BatteryVoltage, options);
            }

            if (value.TxPower != null) {
                writer.WritePropertyName("txPower");
                JsonSerializer.Serialize(writer, value.TxPower, options);
            }

            if (value.MovementCounter != null) {
                writer.WritePropertyName("movementCounter");
                JsonSerializer.Serialize(writer, value.MovementCounter, options);
            }

            if (value.MeasurementSequence != null) {
                writer.WritePropertyName("measurementSequence");
                JsonSerializer.Serialize(writer, value.MeasurementSequence, options);
            }

            writer.WriteEndObject();
        }
    }
}
