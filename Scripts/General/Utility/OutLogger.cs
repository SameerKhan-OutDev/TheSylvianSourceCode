using UnityEngine;
using System.IO;
using System.Runtime.CompilerServices;

/// <summary>
/// Custom logger that prefixes messages with the calling script's name and allows for color coding.
/// </summary>
public static class OutLogger
{
    // Default color for standard logs. You can use standard names (cyan, red) or hex (#00FF00).
    private const string DefaultColor = "cyan";


    /// <summary>
    /// Logs a message with the calling script's name as a prefix, color-coded for better visibility in the Unity Console.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="color"></param>
    /// <param name="filePath"></param>
    public static void Log(object message, string color = DefaultColor, [CallerFilePath] string filePath = "")
    {
        string scriptName = Path.GetFileNameWithoutExtension(filePath);
        Debug.Log($"<color={color}><b>[{scriptName}]</b></color> {message}");
    }

    /// <summary>
    /// warning version of Log, with yellow color and a warning prefix. Useful for highlighting potential issues without marking them as errors.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="filePath"></param>
    public static void LogWarning(object message, [CallerFilePath] string filePath = "")
    {
        string scriptName = Path.GetFileNameWithoutExtension(filePath);
        Debug.LogWarning($"<color=yellow><b>[{scriptName}]</b></color> {message}");
    }


    /// <summary>
    /// for logging errors, with red color and an error prefix. Use this for critical issues that need immediate attention.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="filePath"></param>
    public static void LogError(object message, [CallerFilePath] string filePath = "")
    {
        string scriptName = Path.GetFileNameWithoutExtension(filePath);
        Debug.LogError($"<color=red><b>[{scriptName}]</b></color> {message}");
    }
}