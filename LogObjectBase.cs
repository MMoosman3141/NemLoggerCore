using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Dynamic;
using System.Globalization;

namespace NemLoggerCore {
  public abstract class LogObjectBase {
    [JsonIgnore]
    internal static CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;

    [JsonIgnore]
    internal static TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Local;

    [JsonProperty("logTime")]
    public string LogTime { get;} = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZone).ToString("G", Culture);

    [JsonProperty("category")]
    [JsonConverter(typeof(StringEnumConverter))]
    public LogCategories? Category { get; set; } = null;

    [JsonProperty("message")]
    public string Message { get; set; } = null;

    [JsonProperty("callStack")]
    public string CallStack { get; set; } = null;
  }
}
