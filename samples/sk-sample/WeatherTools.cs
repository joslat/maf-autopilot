using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SkSample;

// 🌉 BRIDGEABLE — a [KernelFunction] plugin. In MAF you can reuse this as-is via
// KernelFunction.AsAIFunction(), or rewrite it as a plain [Description] method
// passed to the agent's tools:.
public sealed class WeatherTools
{
    [KernelFunction]
    [Description("Get the current forecast for a city.")]
    public string GetForecast(string city) => $"Sunny in {city}.";

    [KernelFunction]
    [Description("Get the temperature for a city, in Celsius.")]
    public int GetTemperature(string city) => 21;
}
