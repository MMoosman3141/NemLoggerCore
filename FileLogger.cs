using NemLoggerCore;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace NemFileLogger {
  public sealed class FileLogger : ILogger, IDisposable {
    private readonly FileStream _fileStream;
    private readonly StreamWriter _streamWriter;

    /// <summary>
    /// What is the log file being written to.
    /// This is a read only parameter, and can only be set during construction.
    /// The value of this parameter may be different than the value passed to the constructor if the file specified is already in use.
    /// </summary>
    public string LogFilename { get; private set; }

    /// <summary>
    /// Constructor of the object.
    /// Creates the directory and the file for logging.
    /// If the file already exists, but is not in use, it will be overwritten.
    /// If the file exists, but is in use, a new file will be created with a prefix of a counter value.  i.e. log_2.log, log_3.log etc.
    /// </summary>
    /// <param name="filename">Optional: The full path of the log file.  If not specified the name will be based on the process name and will be written to the CommonApplicatonData folder (i.e. C:\ProgramData)</param>
    public FileLogger(string filename = null) {
      //If the filename is null, establish the default directly and the file.
      //If the filename is not null, get the path from the filename.
      LogFilename = filename;
      string dataDir = Path.GetDirectoryName(filename);
      if (string.IsNullOrWhiteSpace(filename)) {
        string processName = Process.GetCurrentProcess().ProcessName;

        string logFileName = $"{processName}.log";
        dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), processName);

        LogFilename = Path.Combine(dataDir, logFileName);
      }

      //Create the directory if needed.
      if (!Directory.Exists(dataDir)) {
        CreateDirectory(dataDir);
      }

      //If the file exists, confirm that it can be overwritten.  Otherwise generate a similar filename.
      LogFilename = DetermineFileName(dataDir, LogFilename);

      _fileStream = new FileStream(LogFilename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
      _streamWriter = new StreamWriter(_fileStream, new UTF8Encoding(false, false)) { AutoFlush = true };
    }

    /// <summary>
    /// Destructor, used to ensure that file handles are properly disposed of.
    /// </summary>
    ~FileLogger() {
      Dispose();
    }

    /// <summary>
    /// Writes a string message to the log file.
    /// </summary>
    /// <param name="message">The message to write to the log file.</param>
    public void Log(string logStr) {
      WriteLogDataAsync(logStr);
    }

    /// <summary>
    /// Disposes of the FileLogger object
    /// </summary>
    public void Dispose() {
      GC.SuppressFinalize(this);
      LogFilename = "";
      _streamWriter?.Dispose();
      _fileStream?.Dispose();
    }

    private void WriteLogDataAsync(string logStr) {
      try {
        _streamWriter.WriteLine(logStr);
      } catch (Exception) {
        throw;
      }
    }

    private void CreateDirectory(string dirPath) {
      //Establish security on the directory so that it is readably by anyone.
      DirectorySecurity dirSec = new DirectorySecurity();
      dirSec.AddAccessRule(
        new FileSystemAccessRule(
          new SecurityIdentifier(WellKnownSidType.WorldSid, null),
          FileSystemRights.FullControl,
          InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
          PropagationFlags.NoPropagateInherit,
          AccessControlType.Allow
        )
      );

      DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
      dirInfo.Create(dirSec);
    }

    private string DetermineFileName(string dataPath, string initialFileName) {
      string fileName = initialFileName;

      if (File.Exists(fileName)) {
        bool cannotOpen;
        int counter = 1;
        string baseFilename = fileName;
        do {
          try {
            using FileStream fileStream = File.Open(fileName, FileMode.Open, FileAccess.Write);
            cannotOpen = false;
          } catch (IOException) {
            counter++;

            string newFilename = $"{Path.GetFileNameWithoutExtension(baseFilename)}_{counter}.log";
            fileName = Path.Combine(dataPath, newFilename);

            if (!File.Exists(LogFilename)) {
              break;
            }

            cannotOpen = true;
          }
        } while (cannotOpen);
      }

      return fileName;
    }

    public override bool Equals(object o) {
      if(o == null) {
        return false;
      }

      if(ReferenceEquals(this, o)) {
        return true;
      }

      if(!(o is FileLogger)) {
        return false;
      }

      FileLogger loggerObj = (FileLogger)o;

      bool ignoreCase = true;
      if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
        ignoreCase = false;
      }      

      if (string.Compare(LogFilename, loggerObj.LogFilename, ignoreCase, LogObjectBase.Culture) != 0) {
        return false;
      }

      return Equals((FileLogger)o);

    }

    public override int GetHashCode() {
      StringComparison comparison = StringComparison.Ordinal;

      if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
        comparison = StringComparison.OrdinalIgnoreCase;
      }

      return string.GetHashCode(LogFilename, comparison);
    }


  }
}
