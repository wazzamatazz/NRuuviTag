// See https://aka.ms/new-console-template for more information
using NRuuviTag;
using NRuuviTag.Listener.Linux;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
CancellationToken token = cancellationTokenSource.Token; 

IRuuviTagListener client = new BlueZListener("hci0");

JsonSerializerOptions _jsonOptions = new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };


await foreach (var sample in client.ListenAsync(token)) {
    var json = JsonSerializer.Serialize(sample, _jsonOptions);

    Console.WriteLine(json);
}
