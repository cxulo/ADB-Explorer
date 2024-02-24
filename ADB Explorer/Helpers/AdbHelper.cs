﻿using ADB_Explorer.Models;
using ADB_Explorer.Resources;
using ADB_Explorer.Services;

namespace ADB_Explorer.Helpers;

internal static class AdbHelper
{
    public static Task<bool> CheckAdbVersion() => Task.Run(() =>
    {
        string adbPath = string.IsNullOrEmpty(Data.Settings.ManualAdbPath)
            ? AdbExplorerConst.ADB_PROCESS
            : $"\"{Data.Settings.ManualAdbPath}\"";

        Data.RuntimeSettings.AdbVersion = ADBService.VerifyAdbVersion(adbPath);
        if (Data.RuntimeSettings.AdbVersion >= AdbExplorerConst.MIN_ADB_VERSION)
            return true;

        App.Current.Dispatcher.Invoke(() =>
        {
            SimpleStackPanel stack = new()
            {
                Spacing = 8,
                Children =
            {
                new TextBlock()
                {
                    TextWrapping = TextWrapping.Wrap,
                    Text = Data.RuntimeSettings.AdbVersion is null ? Strings.S_MISSING_ADB : Strings.S_ADB_VERSION_LOW,
                },
                new TextBlock()
                {
                    TextWrapping = TextWrapping.Wrap,
                    Text = Strings.S_OVERRIDE_ADB,
                },
                new HyperlinkButton()
                {
                    Content = Strings.S_ADB_LEARN_MORE,
                    ToolTip = Links.L_ADB_PAGE,
                    NavigateUri = Links.L_ADB_PAGE,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
            },
            };

            DialogService.ShowDialog(stack, Strings.S_MISSING_ADB_TITLE, DialogService.DialogIcon.Critical);
        });

        return false;
    });

    public static void VerifyProgressRedirection() => Task.Run(() =>
    {
        if (Data.Settings.AdbProgressMethod is not AppSettings.ProgressMethod.Redirection
            || Data.RuntimeSettings.AdbVersion is null)
            return;

        if (string.IsNullOrEmpty(Data.RuntimeSettings.AdbPath))
            Data.RuntimeSettings.AdbPath = Data.Settings.ManualAdbPath;

        string path = $"{Data.AppDataPath}\\{AdbExplorerConst.PROGRESS_REDIRECTION_PATH}";
        if (Security.CalculateWindowsFileHash(path) != Properties.Resources.ProgressRedirectionHash)
        {
            // hash will be null if file does not exist, or is inaccessible
            try
            {
                File.WriteAllBytes(path, Properties.Resources.AdbProgressRedirection);
            }
            catch (Exception e)
            {
                Data.Settings.AdbProgressMethod = AppSettings.ProgressMethod.DiskUsage;

                App.Current.Dispatcher.Invoke(() =>
                    DialogService.ShowMessage(Strings.S_DEPLOY_REDIRECTION_ERROR + e.Message,
                                              Strings.S_DEPLOY_REDIRECTION_TITLE,
                                              DialogService.DialogIcon.Exclamation,
                                              copyToClipboard: true));

                return;
            }
        }

        Data.ProgressRedirectionPath = path;
    });

    public static void MdnsCheck()
    {
        Task.Run(() => Data.MdnsService.State = ADBService.CheckMDNS() ? MDNS.MdnsState.Running : MDNS.MdnsState.NotRunning);
        Task.Run(async () =>
        {
            while (Data.MdnsService.State is MDNS.MdnsState.InProgress)
            {
                App.Current.Dispatcher.Invoke(() => Data.MdnsService.UpdateProgress());

                await Task.Delay(AdbExplorerConst.MDNS_STATUS_UPDATE_INTERVAL);
            }
        });
    }

    public static void EnableMdns() => App.Current.Dispatcher.Invoke(async () =>
    {
        ADBService.IsMdnsEnabled = Data.Settings.EnableMdns;
        if (Data.Settings.EnableMdns)
        {
            Data.QrClass = new();
        }
        else
        {
            if (Data.MdnsService.State is MDNS.MdnsState.Running)
            {
                var result = await DialogService.ShowConfirmation(Strings.S_DISABLE_MDNS,
                                                                  Strings.S_DISABLE_MDNS_TITLE,
                                                                  "Restart ADB Now",
                                                                  cancelText: "Restart Later",
                                                                  icon: DialogService.DialogIcon.Informational);

                if (result.Item1 is ContentDialogResult.Primary)
                    ADBService.KillAdbServer();
            }

            Data.QrClass = null;
            Data.MdnsService.State = MDNS.MdnsState.Disabled;
        }
    });

    public static bool SilentPull(Device device, FilePath file, string windowsPath) 
        => 0 == ADBService.ExecuteAdbCommand("pull",
                                             out _,
                                             out _,
                                             new(), new[]
                                             {
                                                 "-a",
                                                 ADBService.EscapeAdbString(file.FullPath),
                                                 ADBService.EscapeAdbString(windowsPath)
                                             });

    public static bool SilentPush(Device device, string windowsPath, string androidPath)
        => 0 == ADBService.ExecuteAdbCommand("push",
            out _,
            out _,
            new(), new[]
            {
                ADBService.EscapeAdbString(windowsPath),
                ADBService.EscapeAdbString(androidPath)
            });
}
