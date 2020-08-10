using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NemLoggerCore {
  public interface ILogger {
    public void Log(string loggerJsonObj);
    public bool Equals(object o);
    public int GetHashCode();
  }
}
