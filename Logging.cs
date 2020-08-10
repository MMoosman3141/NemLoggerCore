using NemFileLogger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace NemLoggerCore {
  public static class Logging {
    private static readonly HashSet<ILogger> _loggers = new HashSet<ILogger>();
    private static readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings() { 
      NullValueHandling = NullValueHandling.Ignore, 
      TypeNameHandling = TypeNameHandling.Auto 
    };
    private static readonly object _logLock = new object();

    public static LogLevels LoggingLevel {get; set;} = LogLevels.Warning;

    public static TimeZoneInfo TimeZone {
      get => LogObjectBase.TimeZone;
      set => LogObjectBase.TimeZone = value;
    }

    public static CultureInfo Culture {
      get => LogObjectBase.Culture;
      set => LogObjectBase.Culture = value;
    }


    public static void AddLogger(ILogger logger) {
      lock(_logLock) {
        _loggers.Add(logger);
      }
    }

    public static void RemoveLogger(ILogger logger) {
      lock (_logLock) {
        _loggers.Remove(logger);
      }
    }

    public static void Reset() {
      lock (_logLock) {
        _loggers.Clear();
      }
      LoggingLevel = LogLevels.Warning;
      TimeZone = TimeZoneInfo.Local;
      Culture = CultureInfo.CurrentCulture;
    }

    public static void Log(LogObjectBase logObject) {
      // If logObject is null, there's nothing to be logged.  Likewise if there are no loggers nothing can be logged.
      if(logObject == null || _loggers.Count == 0) {
        return;
      }

      LogCategories logCategory = logObject.Category ?? LogCategories.Info;

      if (((int)LoggingLevel & (int)logCategory) == (int)logCategory) {
        lock (_logLock) {
          Parallel.ForEach(_loggers, logger => {
            logger.Log(JsonConvert.SerializeObject(logObject, Formatting.Indented, _serializerSettings));
          });
        }
      }
    }

    public static void Log(string message) {
      Log(message, null, null);
    }
    public static void Log(string message, LogCategories? category) {
      Log(message, category, null);
    }
    public static void Log(string message, LogCategories? category, string callStack) {
      SimpleLogObject logObj = new SimpleLogObject() {
        Message = message,
        Category = category,
        CallStack = callStack
      };
      Log(logObj);
    }

    public static void Log(Exception exception) {
      Log(exception, null);
    }
    public static void Log(Exception exception, LogCategories? category) {
      SimpleLogObject logObj = new SimpleLogObject() {
        Message = exception?.Message,
        Category = category,
        CallStack = exception?.StackTrace
      };
      Log(logObj);

      if(exception.InnerException != null) {
        Log(exception.InnerException, category);
      }
    }

  }
}
