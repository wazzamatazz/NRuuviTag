using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NRuuviTag;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RuuviDataPayload)), 
    JsonSerializable(typeof(IReadOnlyList<RuuviDataPayload>))]
[JsonSerializable(typeof(RuuviTagSample)), 
    JsonSerializable(typeof(IReadOnlyList<RuuviTagSample>))]
[JsonSerializable(typeof(DateTime))]
public partial class RuuviJsonSerializerContext : JsonSerializerContext { }
