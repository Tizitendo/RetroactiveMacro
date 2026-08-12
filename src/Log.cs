using BepInEx.Logging;
using System.Diagnostics;

namespace Logger;

internal static class Log
{
    private static ManualLogSource _logSource;

    internal static void Init(ManualLogSource logSource)
    {
        _logSource = logSource;
    }

    internal static void message(object data) {
        #if DEBUG
		    _logSource.LogInfo(data);
            if(data != null) {
			    RoR2.Chat.AddMessage(data.ToString());
		    } else {
			    RoR2.Chat.AddMessage("Null");
		    }
        #endif
	}

    internal static void Debug(object data) => _logSource.LogDebug(data);
    internal static void Error(object data) => _logSource.LogError(data);
    internal static void Fatal(object data) => _logSource.LogFatal(data);
    internal static void Info(object data)
    {
		StackFrame frame = new StackFrame(1);
		var method = frame.GetMethod();
		_logSource.LogInfo("[" + method.Name + "]: " + data);
    }
    internal static void Message(object data) => message(data);
    internal static void Warning(object data) => _logSource.LogWarning(data);
}