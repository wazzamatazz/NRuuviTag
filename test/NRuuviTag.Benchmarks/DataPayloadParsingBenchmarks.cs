using BenchmarkDotNet.Attributes;

namespace NRuuviTag.Benchmarks;

[MemoryDiagnoser]
public class DataPayloadParsingBenchmarks {
    
    private byte[] _rawDataV2Valid = null!;
    
    private byte[] _extendedDataV1Valid = null!;


    [GlobalSetup]
    public void Setup() {
        // See https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-5-rawv2#case-valid-data
        _rawDataV2Valid = Convert.FromHexString("0512FC5394C37C0004FFFC040CAC364200CDCBB8334C884F");
        // https://docs.ruuvi.com/communication/bluetooth-advertisements/data-format-e1#case-valid-data
        _extendedDataV1Valid = Convert.FromHexString("E1170C5668C79E0065007004BD11CA00C90A0213E0AC000000DECDEE100000000000CBB8334C884F");
    }
    
    
    [Benchmark]
    public void ParseRawV1Data() => RuuviTagUtilities.ParsePayload(_rawDataV2Valid);
    
    
    [Benchmark]
    public void ParseExtendedV1Data() => RuuviTagUtilities.ParsePayload(_extendedDataV1Valid);

}
