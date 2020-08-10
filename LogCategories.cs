using System;
using System.Collections.Generic;
using System.Text;

namespace NemLoggerCore {
  [Flags]
  public enum LogCategories {
    Info = 0x1,
    Warning = 0x2,
    Error = 0x4,
    Debug = 0x8
  }

  [Flags]
  public enum LogLevels {
    Info = LogCategories.Info,
    Warning = Info | LogCategories.Warning,
    Error = Warning | LogCategories.Error,
    Debug = Error | LogCategories.Debug
  }
}
