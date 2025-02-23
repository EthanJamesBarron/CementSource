using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public static class DownloadManager
{
    public static void DownloadAllModFiles(Action<bool> callback)
    {
        foreach (string path in Directory.GetFiles(CementTools.Cement.MODS_FOLDER_PATH, $"*.{CementTools.Cement.MOD_FILE_EXTENSION}"))
        {
            Task<bool> _dl = DownloadModsFromFile(path);
            _dl.Wait();
            bool succeeded = _dl.Result;
            if (!succeeded)
            {
                callback.Invoke(false);
                return;
            }
        }

        foreach (string path in Directory.GetFiles(CementTools.Cement.HIDDEN_MODS_PATH, $"*.{CementTools.Cement.MOD_FILE_EXTENSION}"))
        {
            Task<bool> _dl = DownloadModsFromFile(path);
            _dl.Wait();
            bool succeeded = _dl.Result;
            if (!succeeded)
            {
                callback.Invoke(false);
                return;
            }
        }
        callback.Invoke(true);
    }

    public static void DownloadMods(string directory)
    {
        foreach (string subDirectory in Directory.GetDirectories(directory))
        {
            DownloadMods(subDirectory);
        }

        foreach (string path in Directory.GetFiles(directory, $"*.{CementTools.Cement.MOD_FILE_EXTENSION}"))
        {
            ThreadPool.QueueUserWorkItem(delegate (object data)
            {
                HandleDownloadMod(path);
            });
        }
    }

    private static async Task<bool> DownloadModFile(string link, string path)
    {
        if (!await DownloadHelper.DownloadFile(link, path, null))
        {
            return false;
        }

        return await DownloadModsFromFile(path);
    }

    private static async Task<bool> DownloadModsFromFile(string path)
    {
        ModFile modFile = ModFile.Get(path);

        string rawLinks = modFile.GetString("Links");
        if (rawLinks == null)
        {
            return false;
        }

        List<ModFile> requiredMods = new List<ModFile>();

        string[] links = rawLinks.Split(',');
        foreach (string link in links)
        {
            if (!LinkHelper.IsLinkToMod(link))
            {
                continue;
            }

            string nameFromLink = LinkHelper.GetNameFromLink(link);

            string pathToMod1 = Path.Combine(CementTools.Cement.HIDDEN_MODS_PATH, nameFromLink);
            string pathToMod2 = Path.Combine(CementTools.Cement.MODS_FOLDER_PATH, nameFromLink);

            bool file1Exists = File.Exists(pathToMod1);
            bool file2Exists = File.Exists(pathToMod2);

            bool succeeded = true;
            if (!file1Exists && !file2Exists)
            {
                await DownloadModFile(link, pathToMod1);
            }

            if (!succeeded)
            {
                return false;
            }

            file1Exists = File.Exists(pathToMod1);
            file2Exists = File.Exists(pathToMod2);

            ModFile childFile;
            if (file1Exists)
            {
                childFile = ModFile.Get(pathToMod1);
            }
            else if (file2Exists)
            {
                childFile = ModFile.Get(pathToMod2);
            }
            else
            {
                throw new Exception("this shouldn't happen lol");
            }

            requiredMods.Add(childFile);
            childFile.AddRequiredBy(modFile);
        }

        modFile.SetRequiredMods(requiredMods.ToArray());
        return true;
    }

    private static void HandleDownloadMod(string pathToMod)
    {
        CementTools.Cement.Singleton.totalMods++;
        ModDownloadHandler handler = new ModDownloadHandler(pathToMod);

        CementTools.Cement.Log($"CREATING HANDLER FOR MOD {pathToMod}");

        handler.OnProgress += (float percentage) => CementTools.Cement.Singleton.OnProgress(pathToMod, percentage);
        handler.Download(callback: CementTools.Cement.Singleton.FinishedDownloadingMod);
    }
}