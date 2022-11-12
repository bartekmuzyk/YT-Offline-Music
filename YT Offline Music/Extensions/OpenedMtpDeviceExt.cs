using System;
using System.Linq;
using LibMtpSharp;

namespace YT_Offline_Music.Extensions;

public static class OpenedMtpDeviceExt
{
    private const uint ROOT_DIRECTORY = 0xFFFFFFFF;

    private const uint DEFAULT_STORAGE = 0;
    
    /// <param name="openedMtpDevice"></param>
    /// <param name="name">Directory name</param>
    /// <returns>Folder id</returns>
    public static uint CreateDirectoryInInternalStorage(this OpenedMtpDevice openedMtpDevice, string name) => 
        openedMtpDevice.CreateFolder(name, ROOT_DIRECTORY, DEFAULT_STORAGE);

    public static bool DirectoryExistsInInternalStorage(this OpenedMtpDevice openedMtpDevice, string name) =>
        openedMtpDevice.GetFolderList(DEFAULT_STORAGE).Any(folder => folder.Name == name);

    public static uint GetIdOfDirectoryInInternalStorage(this OpenedMtpDevice openedMtpDevice, string name)
    {
        var matchingFolders = openedMtpDevice.GetFolderList(DEFAULT_STORAGE).Where(folder => folder.Name == name);

        try
        {
            return matchingFolders.First().FolderId;
        }
        catch (InvalidOperationException)
        {
            throw new ArgumentException($"Directory with name \"{name}\" doesn't exist.", nameof(name));
        }
    }
}