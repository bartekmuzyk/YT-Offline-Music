using System;
using System.Collections.Generic;
using System.Linq;
using YT_Offline_Music.Utils;

namespace YT_Offline_Music.YouTube.Downloader;

public class ProgressCollector
{
    private readonly Action<int> progressChangedCallback;
    
    private readonly Dictionary<string, int> progressStates = new();

    public ProgressCollector(Action<int> progressChangedCallback)
    {
        this.progressChangedCallback = progressChangedCallback;
    }

    private int CompleteProgress =>
        Percentage.Calculate(progressStates.Values.Sum(), progressStates.Count * 100);

    public void AddTask(string taskName)
    {
        if (progressStates.ContainsKey(taskName))
            throw new ArgumentException($"Task \"{taskName}\" already exists in this collector.", nameof(taskName));
        
        progressStates[taskName] = 0;
    }

    public void SetProgress(string task, int value)
    {
        if (!progressStates.ContainsKey(task))
            throw new ArgumentException($"No task named \"{task}\" in this collector.", nameof(task));
        
        progressStates[task] = value;
        
        progressChangedCallback(CompleteProgress);
    }
}