﻿// Type: FanartHandler.FanartHandler
// Assembly: FanartHandler, Version=4.0.2.0, Culture=neutral, PublicKeyToken=null
// MVID: 073E8D78-B6AE-4F86-BDE9-3E09A337833B
 
using MediaPortal.Configuration;
using MediaPortal.GUI.Library;
using MediaPortal.Music.Database;
using MediaPortal.Player;
using MediaPortal.Profile;
using MediaPortal.Services;

using Microsoft.Win32;

using NLog;
using NLog.Config;
using NLog.Targets;

using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Timers;
using System.Xml;
using System.Xml.XPath;

using Timer = System.Timers.Timer;

namespace FanartHandler
{
  public class FanartHandler
  {
    private readonly Logger logger = LogManager.GetCurrentClassLogger();
    private string fhThreadPriority = "Lowest";
    private const string LogFileName = "FanartHandler.log";
    private const string OldLogFileName = "FanartHandler.bak";

    internal int SyncPointDirectory;
    internal int SyncPointDirectoryUpdate;
    internal int SyncPointRefresh;
    internal int SyncPointScraper;
    internal int SyncPointPictures;
    internal int SyncPointDefaultBackdrops;

    internal int syncPointProgressChange;
    internal Hashtable DirectoryTimerQueue;

    private Timer refreshTimer;
    private TimerCallback myScraperTimer;
    private System.Threading.Timer scraperTimer;

    internal FanartPlaying FPlay;
    internal FanartPlayOther FPlayOther;
    internal FanartSelected FSelected;
    internal FanartSelectedOther FSelectedOther;
    internal FanartRandom FRandom;

    private DirectoryWorker MyDirectoryWorker;
    private RefreshWorker MyRefreshWorker;
    private PicturesWorker MyPicturesWorker;
    private DefaultBackdropWorker MyDefaultBackdropWorker;

    internal FileSystemWatcher MyFileWatcher { get; set; }
    internal ScraperNowWorker MyScraperNowWorker { get; set; }
    internal ScraperWorker MyScraperWorker { get; set; }

    internal string FHThreadPriority
    {
      get { return fhThreadPriority; }
      set { fhThreadPriority = value; }
    }

    private void MyFileWatcher_Created(object sender, FileSystemEventArgs e)
    {
      var FileName = e.FullPath ;

      if (Utils.IsJunction)
      {
        if (FileName.Contains(Utils.JunctionTarget, StringComparison.OrdinalIgnoreCase))
        {
          var str = FileName.Replace(Utils.JunctionTarget, Utils.JunctionSource) ;
          // logger.Debug("MyFileWatcher: Revert junction: "+FileName+" -> "+str);
          FileName = str ;
        }
      }

      if (!FileName.Contains(Utils.FAHMusicArtists, StringComparison.OrdinalIgnoreCase) &&
          !FileName.Contains(Utils.FAHMusicAlbums, StringComparison.OrdinalIgnoreCase) &&
          !FileName.Contains(Utils.FAHFolder, StringComparison.OrdinalIgnoreCase) &&
          !FileName.Contains(Utils.FAHTVSeries, StringComparison.OrdinalIgnoreCase) &&
          !FileName.Contains(Utils.FAHMovingPictures, StringComparison.OrdinalIgnoreCase))
        return;

      if (FileName.Contains(Utils.FAHSMusic, StringComparison.OrdinalIgnoreCase) || 
          FileName.Contains(Utils.FAHMusicArtists, StringComparison.OrdinalIgnoreCase) ||
          FileName.Contains(Utils.FAHMusicAlbums, StringComparison.OrdinalIgnoreCase))
        if ((MyScraperWorker != null && MyScraperWorker.IsBusy) || (MyScraperNowWorker != null && MyScraperNowWorker.IsBusy))
          return;

      if (FileName.Contains(Utils.FAHSMovies, StringComparison.OrdinalIgnoreCase) && (MyScraperWorker != null && MyScraperWorker.IsBusy))
        return;

      logger.Debug("MyFileWatcher: Created: "+FileName);
      AddToDirectoryTimerQueue(FileName);
    }

    internal bool CheckValidWindowIDForFanart()
    {
      return (FPlay.CheckValidWindowIDForFanart() || FPlayOther.CheckValidWindowIDForFanart() || FSelected.CheckValidWindowIDForFanart() || FSelectedOther.CheckValidWindowIDForFanart() || FRandom.CheckValidWindowIDForFanart());
    }

    internal bool CheckValidWindowsForDirectoryTimerQueue()
    {
      var flag = false;
      try
      {
        if (!Utils.GetIsStopping())
        {
          flag = (CheckValidWindowIDForFanart() && Utils.AllowFanartInActiveWindow());
        }
      }
      catch (Exception ex)
      {
        logger.Error("CheckValidWindowsForDirectoryTimerQueue: " + ex);
      }
      return flag;
    }

    internal void AddToDirectoryTimerQueue(string param)
    {
      bool flag = false;
      try
      {
        if (CheckValidWindowsForDirectoryTimerQueue())
        {
          flag = UpdateDirectoryTimer(param, "None");
        }

        if (!flag)
        {
          if (DirectoryTimerQueue.Contains(param))
          {
            return;
          }
          DirectoryTimerQueue.Add(param, param);
        }
      }
      catch (Exception ex)
      {
        logger.Error("AddToDirectoryTimerQueue: " + ex);
      }
    }

    internal bool UpdateDirectoryTimer(string param, string type)
    {
      bool flag = false;
      try
      {
        if (Interlocked.CompareExchange(ref SyncPointDirectory, 1, 0) == 0 && (MyDirectoryWorker == null || (MyDirectoryWorker != null && !MyDirectoryWorker.IsBusy)))
        {
          if (MyDirectoryWorker == null)
          {
            MyDirectoryWorker = new DirectoryWorker();
            MyDirectoryWorker.ProgressChanged += new ProgressChangedEventHandler(MyDirectoryWorker.OnProgressChanged);
            MyDirectoryWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(MyDirectoryWorker.OnRunWorkerCompleted);
          }
          if (MyDirectoryWorker != null && !MyDirectoryWorker.IsBusy)
          {
            MyDirectoryWorker.RunWorkerAsync(new string[2] { param, type });
            flag = true;
          }
        }
      }
      catch (Exception ex)
      {
        SyncPointDirectory = 0;
        logger.Error("UpdateDirectoryTimer: " + ex);
      }
      return flag;
    }

    private void ProcessDirectoryTimerQueue()
    {
      var hashtable = new Hashtable();
      foreach (string value in DirectoryTimerQueue.Values)
      {
        if (CheckValidWindowsForDirectoryTimerQueue())
        {
          if (UpdateDirectoryTimer(value, "None"))
          {
            hashtable.Add(value, value);
          }
        }
      }

      foreach (string value in hashtable.Values)
      {
        DirectoryTimerQueue.Remove(value);
      }

      if (hashtable != null)
        hashtable.Clear();
      hashtable = null;
    }

    private void UpdateImageTimer(object stateInfo, ElapsedEventArgs e)
    {
      if (Utils.GetIsStopping())
        return;

      try
      {
        if (Interlocked.CompareExchange(ref SyncPointRefresh, 1, 0) == 0) // && SyncPointDirectoryUpdate == 0) ajs
        {
          if (MyRefreshWorker == null)
          {
            MyRefreshWorker = new RefreshWorker();
            MyRefreshWorker.ProgressChanged += new ProgressChangedEventHandler(MyRefreshWorker.OnProgressChanged);
            MyRefreshWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(MyRefreshWorker.OnRunWorkerCompleted);
          }
          if (MyRefreshWorker != null && !MyRefreshWorker.IsBusy)
          {
            MyRefreshWorker.RunWorkerAsync();
          }
        }
      }
      catch (Exception ex)
      {
        logger.Error("UpdateImageTimer: " + ex);
      }
    }

    internal void UpdateScraperTimer(object stateInfo)
    {
      if (Utils.GetIsStopping())
        return;

      if (!Utils.ScraperMPDatabase || Utils.IsScraping)
      {
        return;
      }

      try
      {
        StartScraper();
      }
      catch (Exception ex)
      {
        logger.Error("UpdateScraperTimer: " + ex);
      }
    }

    private void CheckRefreshCounters()
    {
      if (FSelected.RefreshTickCount > Utils.MaxRefreshTickCount)
      {
        FSelected.RefreshTickCount = 0;
      }

      if (FSelectedOther.RefreshTickCount > Utils.MaxRefreshTickCount)
      {
        FSelectedOther.RefreshTickCount = 0;
      }

      if (FPlay.RefreshTickCount > Utils.MaxRefreshTickCount)
      {
        FPlay.RefreshTickCount = 0;
      }

      if (FPlayOther.RefreshTickCount > Utils.MaxRefreshTickCount)
      {
        FPlayOther.RefreshTickCount = 0;
      }

      if (FRandom.RefreshTickCount > Utils.MaxRefreshTickCount)
      {
        FRandom.RefreshTickCount = 0;
      }
    }

    internal void UpdateDummyControls()
    {
      try
      {
        CheckRefreshCounters();
        int needClean = Utils.MaxRefreshTickCount / 2;

        // Playing
        if (FPlay.RefreshTickCount == 2)
        {
          FPlay.UpdateProperties();
          FPlay.ShowImagePlay();
        }
        else if (FPlay.RefreshTickCount == needClean)
        {
          FPlay.EmptyAllPlayImages();
        }
        if (FPlayOther.RefreshTickCount == 2)
        {
          FPlayOther.ShowImagePlay();
        }

        // Select
        if (FSelected.RefreshTickCount == 2)
        {
          FSelected.UpdateProperties();
          FSelected.ShowImageSelected();
        }
        else if (FSelected.RefreshTickCount == needClean)
        {
          FSelected.EmptyAllSelectedImages();
        }
        if (FSelectedOther.RefreshTickCount == 2)
        {
          FSelectedOther.ShowImageSelected();
        }

        // Random
        if (FRandom.RefreshTickCount == 2)
        {
          FRandom.UpdateProperties();
          FRandom.ShowImageRandom();
        }
        else if (FRandom.RefreshTickCount == needClean)
        {
          FRandom.EmptyAllRandomImages();
        }
      }
      catch (Exception ex)
      {
        logger.Error("UpdateDummyControls: " + ex);
      }
    }

    internal void HideDummyControls()
    {
      try
      {
        FPlay.FanartIsNotAvailablePlay();
        FPlay.HideImagePlay();

        FPlayOther.HideImagePlay();

        FSelected.FanartIsNotAvailable();
        FSelected.HideImageSelected();

        FSelectedOther.HideImageSelected();

        FRandom.FanartIsNotAvailableRandom();
        FRandom.HideImageRandom();
      }
      catch (Exception ex)
      {
        logger.Error("HideDummyControls: " + ex);
      }
    }

    internal void InitRandomProperties()
    {
      if (Utils.GetIsStopping())
        return;

      try
      {
        if (Utils.ContainsID(FRandom.WindowsUsingFanartRandom, (int)GUIWindow.Window.WINDOW_SECOND_HOME)) // If random used in Basic Home ...
        {
          FRandom.RefreshRandomFilenames();
        }
      }
      catch (Exception ex)
      {
        logger.Error("InitRandomProperties: " + ex);
      }
    }

    public void EmptyGlobalProperties()
    {
      Utils.SetProperty("scraper.task", string.Empty);
      Utils.SetProperty("scraper.percent.completed", string.Empty);
      Utils.SetProperty("scraper.percent.sign", string.Empty);
      Utils.SetProperty("pictures.slideshow.translation", Translation.FHSlideshow);
      Utils.SetProperty("pictures.slideshow.enabled", (Utils.UseMyPicturesSlideShow ? "true" : "false"));
    }

    public void EmptyAllProperties()
    {
      EmptyGlobalProperties();
      FPlay.EmptyAllPlayProperties();
      FPlayOther.EmptyAllPlayProperties();
      FSelected.EmptyAllSelectedProperties();
      FSelectedOther.EmptyAllSelectedProperties();
      FRandom.EmptyAllRandomProperties();
    }

    public void ClearCurrProperties()
    {
      FPlay.ClearCurrProperties();
      FPlayOther.ClearCurrProperties();
      FSelected.ClearCurrProperties();
      FSelectedOther.ClearCurrProperties();
      FRandom.ClearCurrProperties();
    }

    public void RefreshRefreshTickCount()
    {
      FPlay.RefreshRefreshTickCount();
      FPlayOther.RefreshRefreshTickCount();
      FSelected.RefreshRefreshTickCount();
      FSelectedOther.RefreshRefreshTickCount();
      FRandom.RefreshRefreshTickCount();
    }

    private void SetupVariables()
    {
      Utils.SetIsStopping(false);
      
      SyncPointRefresh = 0;
      SyncPointDirectory = 0;
      SyncPointDirectoryUpdate = 0;
      SyncPointScraper = 0;
      SyncPointPictures = 0;
      SyncPointDefaultBackdrops = 0;

      DirectoryTimerQueue = new Hashtable();
      Utils.DefaultBackdropImages = new Hashtable();
      Utils.SlideShowImages = new Hashtable();
    }

    private void InitLogger()
    {
      var loggingConfiguration = LogManager.Configuration ?? new LoggingConfiguration();
      try
      {
        var fileInfo = new FileInfo(Config.GetFile((Config.Dir) 1, LogFileName));
        if (fileInfo.Exists)
        {
          if (File.Exists(Config.GetFile((Config.Dir) 1, OldLogFileName)))
            File.Delete(Config.GetFile((Config.Dir) 1, OldLogFileName));
          fileInfo.CopyTo(Config.GetFile((Config.Dir) 1, OldLogFileName));
          fileInfo.Delete();
        }
      }
      catch { }

      var fileTarget = new FileTarget();
      fileTarget.FileName = Config.GetFile((Config.Dir) 1, LogFileName);
      fileTarget.Encoding = "utf-8";
      fileTarget.Layout = "${date:format=dd-MMM-yyyy HH\\:mm\\:ss} ${level:fixedLength=true:padding=5} [${logger:fixedLength=true:padding=20:shortName=true}]: ${message} ${exception:format=tostring}";
      loggingConfiguration.AddTarget("file", fileTarget);
      var settings = new Settings(Config.GetFile((Config.Dir) 10, "MediaPortal.xml"));
      var str = settings.GetValue("general", "ThreadPriority");
      FHThreadPriority = str == null || !str.Equals("Normal", StringComparison.CurrentCulture) ? (str == null || !str.Equals("BelowNormal", StringComparison.CurrentCulture) ? "BelowNormal" : "Lowest") : "Lowest";
      LogLevel minLevel;
      switch ((int) (Level) settings.GetValueAsInt("general", "loglevel", 0))
      {
        case 0:
          minLevel = LogLevel.Error;
          break;
        case 1:
          minLevel = LogLevel.Warn;
          break;
        case 2:
          minLevel = LogLevel.Info;
          break;
        default:
          minLevel = LogLevel.Debug;
          break;
      }
      var loggingRule = new LoggingRule("*", minLevel, fileTarget);
      loggingConfiguration.LoggingRules.Add(loggingRule);
      LogManager.Configuration = loggingConfiguration;
    }

    internal void Start()
    {
      try
      {
        Utils.DelayStop = new Hashtable();
        Utils.SetIsStopping(false);
        //
        InitLogger();
        //
        logger.Info("Fanart Handler is starting.");
        logger.Info("Fanart Handler version is " + Utils.GetAllVersionNumber());
        //
        Translation.Init();
        SetupConfigFile();
        Utils.InitFolders();
        Utils.LoadSettings();
        //
        FPlay = new FanartPlaying();
        FPlayOther = new FanartPlayOther();
        FSelected = new FanartSelected();
        FSelectedOther = new FanartSelectedOther();
        FRandom = new FanartRandom();
        //
        SetupWindowsUsingFanartHandlerVisibility();
        SetupVariables();
        Utils.SetupDirectories();
        //
        logger.Debug("Default Backdrops [" + Utils.UseDefaultBackdrop + " - " + Utils.DefaultBackdropMask+"] for Music" + (Utils.DefaultBackdropIsImage ? ":"+Utils.DefaultBackdrop : "."));
        if (Utils.DefaultBackdropIsImage)
        {
          Utils.DefaultBackdropImages.Add(0, new FanartImage("", "", Utils.DefaultBackdrop, "", "", ""));
        }
        else
        {
          if (Utils.UseDefaultBackdrop)
          {
            if (!Utils.GetIsStopping() && SyncPointDefaultBackdrops == 0)
            {
              MyDefaultBackdropWorker = new DefaultBackdropWorker();
              MyDefaultBackdropWorker.ProgressChanged += new ProgressChangedEventHandler(MyDefaultBackdropWorker.OnProgressChanged);
              MyDefaultBackdropWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(MyDefaultBackdropWorker.OnRunWorkerCompleted);
              MyDefaultBackdropWorker.RunWorkerAsync();
            }
          }
        }
        logger.Debug("MyPictures SlideShow: "+Utils.Check(Utils.UseMyPicturesSlideShow));
        if (Utils.UseMyPicturesSlideShow)
        {
          if (!Utils.GetIsStopping() && SyncPointPictures == 0)
          {
            MyPicturesWorker = new PicturesWorker();
            MyPicturesWorker.ProgressChanged += new ProgressChangedEventHandler(MyPicturesWorker.OnProgressChanged);
            MyPicturesWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(MyPicturesWorker.OnRunWorkerCompleted);
            MyPicturesWorker.RunWorkerAsync();
          }
        }
        //
        logger.Debug("FanartHandler skin use: " + Utils.Check(FPlayOther.WindowsUsingFanartPlayClearArt.Count > 0) + " Play ClearArt, " + 
                                                  Utils.Check(FPlayOther.WindowsUsingFanartPlayGenre.Count > 0) + " Play Genres");
        logger.Debug("                        " + Utils.Check(FSelectedOther.WindowsUsingFanartSelectedClearArtMusic.Count > 0) + " Selected Music ClearArt, " + 
                                                  Utils.Check(FSelectedOther.WindowsUsingFanartSelectedGenreMusic.Count > 0) + " Selected Music Genres");
        logger.Debug("                        " + Utils.Check(FSelectedOther.WindowsUsingFanartSelectedStudioMovie.Count > 0) + " Selected Movie Studios, " + 
                                                  Utils.Check(FSelectedOther.WindowsUsingFanartSelectedGenreMovie.Count > 0) + " Selected Movie Genres, " +
                                                  Utils.Check(FSelectedOther.WindowsUsingFanartSelectedAwardMovie.Count > 0) + " Selected Movie Awards");
        //
        Utils.InitiateDbm("mediaportal");
        Utils.StopScraper = false;
        //
        AddToDirectoryTimerQueue("All");
        //
        SystemEvents.PowerModeChanged += new PowerModeChangedEventHandler(OnSystemPowerModeChanged);
        //
        GUIWindowManager.OnActivateWindow += new GUIWindowManager.WindowActivationHandler(GuiWindowManagerOnActivateWindow);
        GUIWindowManager.Receivers += new SendMessageHandler(GUIWindowManager_OnNewMessage);
        //
        g_Player.PlayBackStarted += new g_Player.StartedHandler(OnPlayBackStarted);
        g_Player.PlayBackEnded += new g_Player.EndedHandler(OnPlayBackEnded);
        //
        refreshTimer = new Timer();
        refreshTimer.Interval = Utils.RefreshTimerInterval;
        refreshTimer.Elapsed += new ElapsedEventHandler(UpdateImageTimer);
        //
        if (Utils.ScraperMPDatabase)
        {
          myScraperTimer = new TimerCallback(UpdateScraperTimer);
          scraperTimer = new System.Threading.Timer(myScraperTimer, null, 1000, Utils.ScrapperTimerInterval);
        }
        //
        InitFileWatcher();
        try
        {
          UtilsMovingPictures.SetupMovingPicturesLatest();
        }
        catch { }
        //
        try
        {
          UtilsTVSeries.SetupTVSeriesLatest();
        }
        catch { }
        //
        ClearCurrProperties();
        EmptyAllProperties();
        HideScraperProgressIndicator();
        HideDummyControls();
        InitRandomProperties();
        //
        logger.Info("Fanart Handler is started.");
        logger.Debug("Current Culture: {0}", CultureInfo.CurrentCulture.Name);
      }
      catch (Exception ex)
      {
        logger.Error("Start: " + ex);
      }
      Utils.iActiveWindow = GUIWindowManager.ActiveWindow;
    }

    private void SetupConfigFile()
    {
    }

    private void InitFileWatcher()
    {
      try
      {
        MyFileWatcher = new FileSystemWatcher();
        MyFileWatcher.Path = Utils.FAHWatchFolder;
        MyFileWatcher.Filter = "*.jpg";
        MyFileWatcher.IncludeSubdirectories = true;
        MyFileWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite;
        MyFileWatcher.Created += new FileSystemEventHandler(MyFileWatcher_Created);
        MyFileWatcher.EnableRaisingEvents = true;
      }
      catch (Exception ex)
      {
        logger.Error("InitFileWatcher: "+ex);
      }
    }

    private void GUIWindowManager_OnNewMessage(GUIMessage message)
    {
      switch (message.Message)
      {
        case GUIMessage.MessageType.GUI_MSG_VIDEOINFO_REFRESH:
        {
          logger.Debug("VideoInfo refresh detected: Refreshing video fanarts.");
          AddToDirectoryTimerQueue(Utils.FAHSMovies);
          break;
        }
        case GUIMessage.MessageType.GUI_MSG_PLAYBACK_STOPPED:
        case GUIMessage.MessageType.GUI_MSG_PLAYBACK_ENDED:
        case GUIMessage.MessageType.GUI_MSG_STOP_FILE:
        {
          logger.Debug("Stop playback message recieved: "+message.Message.ToString());
          FPlay.EmptyAllPlayProperties();
          FPlayOther.EmptyAllPlayProperties();
          break;
        }
      }
    }

    private void OnSystemPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
      try
      {
        if (e.Mode == PowerModes.Resume)
        {
          logger.Info("Fanart Handler: is resuming from standby/hibernate.");
          Utils.InitiateDbm("mediaportal");
          // StopTasks(false);
          // Start();
          UpdateDirectoryTimer("All", "Resume");
        }
        else
        {
          if (e.Mode != PowerModes.Suspend)
            return;
          logger.Info("Fanart Handler: is suspending/hibernating...");
          if (Utils.GetDbm() != null)
            Utils.GetDbm().Close();
          // StopTasks(true);
          logger.Info("Fanart Handler: is suspended/hibernated.");
        }
      }
      catch (Exception ex)
      {
        logger.Error("OnSystemPowerModeChanged: " + ex);
      }
    }

    internal void CheckRefreshTimer()
    {
      try
      {
        if (Utils.iActiveWindow == (int)GUIWindow.Window.WINDOW_INVALID)
        {
          return;
        }

        if (Utils.IsScraping)
        {
          ShowScraperProgressIndicator(); 
        }
        else
        {
          Utils.TotArtistsBeingScraped = 0.0;
          Utils.CurrArtistsBeingScraped = 0.0;
          HideScraperProgressIndicator();
        }

        bool refreshStart = false;

        if ((CheckValidWindowIDForFanart() || Utils.UseOverlayFanart))
        {
          // Selected
          if (FSelected.CheckValidWindowIDForFanart() && Utils.AllowFanartInActiveWindow())
          {
            // logger.Debug("*** Activate Window:" + Utils.sActiveWindow + " - Selected");
            refreshStart = true ;
          }
          else
          {
            FSelected.EmptyAllProperties();
          }

          if (FSelectedOther.CheckValidWindowIDForFanart())
          {
            // logger.Debug("*** Activate Window:" + Utils.sActiveWindow + " - Selected (Other)");
            refreshStart = true ;
          }
          else
          {
            FSelectedOther.EmptyAllProperties();
          }

          // Play
          if ((FPlay.CheckValidWindowIDForFanart() || Utils.UseOverlayFanart) && 
              (g_Player.Playing || g_Player.Paused) && (g_Player.IsCDA || g_Player.IsMusic || g_Player.IsRadio) && 
              Utils.AllowFanartInActiveWindow())
          {
            // logger.Debug("*** Activate Window:" + Utils.sActiveWindow + " - Play");
            refreshStart = true ;
          }
          else
          {
            if (FPlay.IsPlaying)
            {
              StopScraperNowPlaying();
            }
            FPlay.EmptyAllProperties();
          }

          if (FPlayOther.CheckValidWindowIDForFanart())
          {
            // logger.Debug("*** Activate Window:" + Utils.sActiveWindow + " - Play (Other)");
            refreshStart = true ;
          }
          else
          {
            FPlayOther.EmptyAllProperties();
          }

          // Random
          if (FRandom.CheckValidWindowIDForFanart() && Utils.AllowFanartInActiveWindow())
          {
            // logger.Debug("*** Activate Window:" + Utils.sActiveWindow + " - Random");
            refreshStart = true ;
          }
          else
          {
            FRandom.EmptyAllProperties();
          }
        }

        if (refreshStart)
        {
          StartRefreshTimer();
        }
        else
        {
          StopRefreshTimer();
        }
      }
      catch (Exception ex)
      {
        logger.Error("CheckRefreshTimer: " + ex.ToString());
      }
    }

    internal void StartRefreshTimer()
    {
      if (refreshTimer != null && !refreshTimer.Enabled)
      {
        refreshTimer.Start();
        // logger.Debug("*** Refresh timer start...");
      }
    }

    internal void StopRefreshTimer()
    {
      if (refreshTimer != null && refreshTimer.Enabled)
      {
        refreshTimer.Stop();
        // logger.Debug("*** Refresh timer stop...");
      }
      if (FPlay.IsPlaying)
      {
        StopScraperNowPlaying();
      }

      EmptyAllProperties();
      HideDummyControls();

      System.Threading.ThreadPool.QueueUserWorkItem(delegate { FRandom.RefreshRandomFilenames(); }, null);
    }

    internal void GuiWindowManagerOnActivateWindow(int activeWindowId)
    {
      Utils.iActiveWindow = activeWindowId;

      try
      {
        RefreshRefreshTickCount();
        ClearCurrProperties();
        CheckRefreshTimer();
        ProcessDirectoryTimerQueue();
      }
      catch (Exception ex)
      {
        logger.Error("GuiWindowManagerOnActivateWindow: " + ex);
      }
    }

    internal void OnPlayBackStarted(g_Player.MediaType type, string filename)
    {
      try
      {
        FPlay.IsPlaying = true;
        FPlay.AddPlayingArtistPropertys(string.Empty, string.Empty, string.Empty);
        FPlayOther.AddPlayingArtistPropertys(string.Empty, string.Empty, string.Empty);
        if (type == g_Player.MediaType.Music || type == g_Player.MediaType.Radio || MediaPortal.Util.Utils.IsLastFMStream(filename))
        {
          if ((Utils.ContainsID(FPlay.WindowsUsingFanartPlay) || 
               Utils.ContainsID(FPlayOther.WindowsUsingFanartPlayGenre) || 
               Utils.ContainsID(FPlayOther.WindowsUsingFanartPlayClearArt) || 
               Utils.UseOverlayFanart) && Utils.AllowFanartInActiveWindow())
          {
            StartRefreshTimer();
          }
        }
      }
      catch (Exception ex)
      {
        logger.Error("OnPlayBackStarted: " + ex.ToString());
      }
    }

    internal void OnPlayBackEnded(g_Player.MediaType type, string filename)
    {
      try
      {
        FPlay.AddPlayingArtistPropertys(string.Empty, string.Empty, string.Empty);
        FPlayOther.AddPlayingArtistPropertys(string.Empty, string.Empty, string.Empty);
        StartRefreshTimer();
      }
      catch (Exception ex)
      {
        logger.Error("OnPlayBackEnded: " + ex.ToString());
      }
    }

    private void StartScraper()
    {
      try
      {
        if (Utils.GetIsStopping())
          return;

        if (MyScraperWorker == null)
        {
          MyScraperWorker = new ScraperWorker();
          MyScraperWorker.ProgressChanged += new ProgressChangedEventHandler(MyScraperWorker.OnProgressChanged);
          MyScraperWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(MyScraperWorker.OnRunWorkerCompleted);
        }
        if (MyScraperWorker.IsBusy)
          return;

        MyScraperWorker.RunWorkerAsync();
      }
      catch (Exception ex)
      {
        logger.Error("StartScraper: " + ex);
      }
    }

    internal void StartScraperNowPlaying(string artist, string album, string genre)
    {
      try
      {
        if (Utils.GetIsStopping())
          return;

        if (MyScraperNowWorker == null)
        {
          MyScraperNowWorker = new ScraperNowWorker();
          MyScraperNowWorker.ProgressChanged += new ProgressChangedEventHandler(MyScraperNowWorker.OnProgressChanged);
          MyScraperNowWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(MyScraperNowWorker.OnRunWorkerCompleted);
        }
        if (MyScraperNowWorker.IsBusy)
          return;

        MyScraperNowWorker.RunWorkerAsync(new string[3]
        {
            artist,
            album, 
            genre
        });
      }
      catch (Exception ex)
      {
        logger.Error("StartScraperNowPlaying: " + ex);
      }
    }

    internal void StopScraperNowPlaying()
    {
      try
      {
        if (MyScraperNowWorker == null)
          return;

        MyScraperNowWorker.CancelAsync();
        MyScraperNowWorker.Dispose();
        Utils.ReleaseDelayStop("FanartHandlerSetup-ScraperNowPlaying");
      }
      catch (Exception ex)
      {
        logger.Error("StopScraperNowPlaying: " + ex);
      }
    }

    internal void Stop()
    {
      try
      {
        StopTasks(false);
        logger.Info("Fanart Handler is stopped.");
      }
      catch (Exception ex)
      {
        logger.Error("Stop: " + ex);
      }
    }

    private void StopTasks(bool suspending)
    {
      try
      {
        Utils.SetIsStopping(true);
        if (Utils.GetDbm() != null)
          Utils.StopScraper = true;

        try
        {
          UtilsMovingPictures.DisposeMovingPicturesLatest();
        }
        catch { }
        try
        {
          UtilsTVSeries.DisposeTVSeriesLatest();
        }
        catch { }

        // ISSUE: method pointer
        GUIWindowManager.OnActivateWindow -= new GUIWindowManager.WindowActivationHandler(GuiWindowManagerOnActivateWindow);
        GUIWindowManager.Receivers -= new SendMessageHandler(GUIWindowManager_OnNewMessage);
        g_Player.PlayBackStarted -= new g_Player.StartedHandler(OnPlayBackStarted);
        g_Player.PlayBackEnded -= new g_Player.EndedHandler(OnPlayBackEnded);

        var num = 0;
        while (Utils.GetDelayStop() && num < 20)
        {
          Utils.ThreadToLongSleep();
          checked { ++num; }
        }

        StopScraperNowPlaying();
        if (MyFileWatcher != null)
        {
          MyFileWatcher.Created -= new FileSystemEventHandler(MyFileWatcher_Created);
          MyFileWatcher.Dispose();
        }
        if (scraperTimer != null)
        {
          scraperTimer.Dispose();
        }
        if (refreshTimer != null)
        {
          refreshTimer.Stop();
          refreshTimer.Dispose();
        }
        if (MyScraperWorker != null)
        {
          MyScraperWorker.CancelAsync();
          MyScraperWorker.Dispose();
        }
        if (MyScraperNowWorker != null)
        {
          MyScraperNowWorker.CancelAsync();
          MyScraperNowWorker.Dispose();
        }
        if (MyDirectoryWorker != null)
        {
          MyDirectoryWorker.CancelAsync();
          MyDirectoryWorker.Dispose();
        }
        if (MyRefreshWorker != null)
        {
          MyRefreshWorker.CancelAsync();
          MyRefreshWorker.Dispose();
        }
        if (MyPicturesWorker != null)
        {
          MyPicturesWorker.CancelAsync();
          MyPicturesWorker.Dispose();
        }
        if (MyDefaultBackdropWorker != null)
        {
          MyDefaultBackdropWorker.CancelAsync();
          MyDefaultBackdropWorker.Dispose();
        }
        if (Utils.GetDbm() != null)
          Utils.GetDbm().Close();

        if (FPlay != null)
          FPlay.EmptyAllPlayImages();
        if (FSelected != null)
          FSelected.EmptyAllSelectedImages();
        if (FRandom != null)
        {
          FRandom.EmptyAllRandomImages();
          FRandom.ClearPropertiesRandom();
        }
        Logos.ClearDynLogos();
        //
        if (!suspending)
        {
          SystemEvents.PowerModeChanged -= new PowerModeChangedEventHandler(OnSystemPowerModeChanged);
        }
        //
        Utils.BadArtistsList = null;  
        Utils.MyPicturesSlideShowFolders = null;  
        Utils.Genres = null;
        Utils.Characters = null;
        Utils.Studios = null;
        Utils.AwardsList = null;
        //
        FPlay = null;
        FPlayOther = null;
        FSelected = null;
        FSelectedOther = null;
        FRandom = null;
        //
        Utils.DelayStop = new Hashtable();
      }
      catch (Exception ex)
      {
        logger.Error("Stop: " + ex);
      }
    }

    internal void ShowScraperProgressIndicator()
    {
      if (Utils.iActiveWindow > (int)GUIWindow.Window.WINDOW_INVALID)
      {
        GUIControl.ShowControl(Utils.iActiveWindow, 91919280);
      }
    }

    internal void HideScraperProgressIndicator()
    {
      if (Utils.iActiveWindow > (int)GUIWindow.Window.WINDOW_INVALID)
      {
        GUIControl.HideControl(Utils.iActiveWindow, 91919280);
      }
      EmptyGlobalProperties();
    }

    #region Setup Windows From Skin File
    private string GetNodeValue(XPathNodeIterator myXPathNodeIterator)
    {
      if (myXPathNodeIterator.Count > 0)
      {
        myXPathNodeIterator.MoveNext();
        if (myXPathNodeIterator.Current != null)
          return myXPathNodeIterator.Current.Value;
      }
      return string.Empty;
    }

    private string ParseNodeValue(string s)
    {
      return !string.IsNullOrEmpty(s) && s.Substring(checked (s.IndexOf(":", StringComparison.CurrentCulture) + 1)).Equals("Yes", StringComparison.CurrentCulture) ? "True" : "False";
    }

    private void SetupWindowsUsingFanartHandlerVisibility(string SkinDir = (string) null, string ThemeDir = (string) null)
    {
      var path = string.Empty;
      var theme = string.Empty; 

      if (string.IsNullOrEmpty(SkinDir))
      {
        path = GUIGraphicsContext.Skin + @"\";
        theme = Utils.GetThemeFolder(path);
        logger.Debug("Scan Skin folder for XML: "+path) ;
      }
      else
      {
        path = ThemeDir;
        logger.Debug("Scan Skin Theme folder for XML: "+path) ;
      }

      var files = new DirectoryInfo(path).GetFiles("*.xml");
      var XMLName = string.Empty;

      foreach (var fileInfo in files)
      {
        try
        {
          XMLName = fileInfo.Name;

          var _flag1Music = false;
          var _flag2Music = false;
          var _flag1ScoreCenter = false;
          var _flag2ScoreCenter = false;
          var _flag1Movie = false;
          var _flag2Movie = false;
          var _flag1Picture = false;
          var _flag2Picture = false;
          var _flagPlay = false;

          var _flagGenrePlay = false;
          var _flagGenrePlayAll = false;
          var _flagGenrePlayVertical = false;

          var _flagGenreMusic = false;
          var _flagGenreMusicAll = false;
          var _flagGenreMusicVertical = false;

          var _flagAwardMovie = false;
          var _flagAwardMovieAll = false;
          var _flagAwardMovieVertical = false;
          var _flagGenreMovie = false;
          var _flagGenreMovieAll = false;
          var _flagGenreMovieVertical = false;
          var _flagStudioMovie = false;
          var _flagStudioMovieAll = false;
          var _flagStudioMovieVertical = false;

          var _flagClearArt = false;
          var _flagClearArtPlay = false;

          var XMLFolder = fileInfo.FullName.Substring(0, fileInfo.FullName.LastIndexOf("\\"));
          var navigator = new XPathDocument(fileInfo.FullName).CreateNavigator();
          var nodeValue = GetNodeValue(navigator.Select("/window/id"));

          if (!string.IsNullOrEmpty(nodeValue))
          {
            HandleXmlImports(fileInfo.FullName, nodeValue, ref _flag1Music, ref _flag2Music, 
                                                           ref _flag1ScoreCenter, ref _flag2ScoreCenter, 
                                                           ref _flag1Movie, ref _flag2Movie, 
                                                           ref _flag1Picture, ref _flag2Picture, 
                                                           ref _flagPlay,
                                                           ref _flagClearArt, ref _flagClearArtPlay,
                                                           ref _flagGenrePlay, ref _flagGenrePlayAll, ref _flagGenrePlayVertical, 
                                                           ref _flagGenreMusic, ref _flagGenreMusicAll, ref _flagGenreMusicVertical, 
                                                           ref _flagGenreMovie, ref _flagGenreMovieAll, ref _flagGenreMovieVertical, 
                                                           ref _flagStudioMovie, ref _flagStudioMovieAll, ref _flagStudioMovieVertical,
                                                           ref _flagAwardMovie, ref _flagAwardMovieAll, ref _flagAwardMovieVertical
                                                           );
            var xpathNodeIterator = navigator.Select("/window/controls/import");
            if (xpathNodeIterator.Count > 0)
            {
              while (xpathNodeIterator.MoveNext())
              {
                var XMLFullName = Path.Combine(XMLFolder, xpathNodeIterator.Current.Value);
                if (File.Exists(XMLFullName))
                {
                  HandleXmlImports(XMLFullName, nodeValue, ref _flag1Music, ref _flag2Music, 
                                                           ref _flag1ScoreCenter, ref _flag2ScoreCenter, 
                                                           ref _flag1Movie, ref _flag2Movie, 
                                                           ref _flag1Picture, ref _flag2Picture, 
                                                           ref _flagPlay, 
                                                           ref _flagClearArt, ref _flagClearArtPlay,
                                                           ref _flagGenrePlay, ref _flagGenrePlayAll, ref _flagGenrePlayVertical, 
                                                           ref _flagGenreMusic, ref _flagGenreMusicAll, ref _flagGenreMusicVertical, 
                                                           ref _flagGenreMovie, ref _flagGenreMovieAll, ref _flagGenreMovieVertical, 
                                                           ref _flagStudioMovie, ref _flagStudioMovieAll, ref _flagStudioMovieVertical,
                                                           ref _flagAwardMovie, ref _flagAwardMovieAll, ref _flagAwardMovieVertical
                                                           );
                  if (!string.IsNullOrEmpty(theme))
                  {
                    XMLFullName = Path.Combine(theme, xpathNodeIterator.Current.Value);
                    if (File.Exists(XMLFullName))
                      HandleXmlImports(XMLFullName, nodeValue, ref _flag1Music, ref _flag2Music, 
                                                               ref _flag1ScoreCenter, ref _flag2ScoreCenter, 
                                                               ref _flag1Movie, ref _flag2Movie, 
                                                               ref _flag1Picture, ref _flag2Picture, 
                                                               ref _flagPlay, 
                                                               ref _flagClearArt, ref _flagClearArtPlay,
                                                               ref _flagGenrePlay, ref _flagGenrePlayAll, ref _flagGenrePlayVertical, 
                                                               ref _flagGenreMusic, ref _flagGenreMusicAll, ref _flagGenreMusicVertical, 
                                                               ref _flagGenreMovie, ref _flagGenreMovieAll, ref _flagGenreMovieVertical, 
                                                               ref _flagStudioMovie, ref _flagStudioMovieAll, ref _flagStudioMovieVertical,
                                                               ref _flagAwardMovie, ref _flagAwardMovieAll, ref _flagAwardMovieVertical
                                                               );
                  }
                }
                else if ((!string.IsNullOrEmpty(SkinDir)) && (!string.IsNullOrEmpty(ThemeDir)))
                {
                  XMLFullName = Path.Combine(SkinDir, xpathNodeIterator.Current.Value);
                  if (File.Exists(XMLFullName))
                    HandleXmlImports(XMLFullName, nodeValue, ref _flag1Music, ref _flag2Music, 
                                                             ref _flag1ScoreCenter, ref _flag2ScoreCenter, 
                                                             ref _flag1Movie, ref _flag2Movie, 
                                                             ref _flag1Picture, ref _flag2Picture, 
                                                             ref _flagPlay, 
                                                             ref _flagClearArt, ref _flagClearArtPlay,
                                                             ref _flagGenrePlay, ref _flagGenrePlayAll, ref _flagGenrePlayVertical, 
                                                             ref _flagGenreMusic, ref _flagGenreMusicAll, ref _flagGenreMusicVertical, 
                                                             ref _flagGenreMovie, ref _flagGenreMovieAll, ref _flagGenreMovieVertical, 
                                                             ref _flagStudioMovie, ref _flagStudioMovieAll, ref _flagStudioMovieVertical,
                                                             ref _flagAwardMovie, ref _flagAwardMovieAll, ref _flagAwardMovieVertical
                                                             );
                }
              }
            }
            xpathNodeIterator = navigator.Select("/window/controls/include");
            if (xpathNodeIterator.Count > 0)
            {
              while (xpathNodeIterator.MoveNext())
              {
                var XMLFullName = Path.Combine(XMLFolder, xpathNodeIterator.Current.Value);
                if (File.Exists(XMLFullName))
                {
                  HandleXmlImports(XMLFullName, nodeValue, ref _flag1Music, ref _flag2Music,
                                                           ref _flag1ScoreCenter, ref _flag2ScoreCenter, 
                                                           ref _flag1Movie, ref _flag2Movie, 
                                                           ref _flag1Picture, ref _flag2Picture, 
                                                           ref _flagPlay, 
                                                           ref _flagClearArt, ref _flagClearArtPlay,
                                                           ref _flagGenrePlay, ref _flagGenrePlayAll, ref _flagGenrePlayVertical, 
                                                           ref _flagGenreMusic, ref _flagGenreMusicAll, ref _flagGenreMusicVertical, 
                                                           ref _flagGenreMovie, ref _flagGenreMovieAll, ref _flagGenreMovieVertical, 
                                                           ref _flagStudioMovie, ref _flagStudioMovieAll, ref _flagStudioMovieVertical,
                                                           ref _flagAwardMovie, ref _flagAwardMovieAll, ref _flagAwardMovieVertical
                                                           );
                  if (!string.IsNullOrEmpty(theme))
                  {
                    XMLFullName = Path.Combine(theme, xpathNodeIterator.Current.Value);
                    if (File.Exists(XMLFullName))
                      HandleXmlImports(XMLFullName, nodeValue, ref _flag1Music, ref _flag2Music, 
                                                               ref _flag1ScoreCenter, ref _flag2ScoreCenter, 
                                                               ref _flag1Movie, ref _flag2Movie, 
                                                               ref _flag1Picture, ref _flag2Picture, 
                                                               ref _flagPlay, 
                                                               ref _flagClearArt, ref _flagClearArtPlay,
                                                               ref _flagGenrePlay, ref _flagGenrePlayAll, ref _flagGenrePlayVertical, 
                                                               ref _flagGenreMusic, ref _flagGenreMusicAll, ref _flagGenreMusicVertical, 
                                                               ref _flagGenreMovie, ref _flagGenreMovieAll, ref _flagGenreMovieVertical, 
                                                               ref _flagStudioMovie, ref _flagStudioMovieAll, ref _flagStudioMovieVertical,
                                                               ref _flagAwardMovie, ref _flagAwardMovieAll, ref _flagAwardMovieVertical
                                                               );
                  }
                }
                else if ((!string.IsNullOrEmpty(SkinDir)) && (!string.IsNullOrEmpty(ThemeDir)))
                {
                  XMLFullName = Path.Combine(SkinDir, xpathNodeIterator.Current.Value);
                  if (File.Exists(XMLFullName))
                    HandleXmlImports(XMLFullName, nodeValue, ref _flag1Music, ref _flag2Music, 
                                                             ref _flag1ScoreCenter, ref _flag2ScoreCenter, 
                                                             ref _flag1Movie, ref _flag2Movie, 
                                                             ref _flag1Picture, ref _flag2Picture, 
                                                             ref _flagPlay, 
                                                             ref _flagClearArt, ref _flagClearArtPlay,
                                                             ref _flagGenrePlay, ref _flagGenrePlayAll, ref _flagGenrePlayVertical, 
                                                             ref _flagGenreMusic, ref _flagGenreMusicAll, ref _flagGenreMusicVertical, 
                                                             ref _flagGenreMovie, ref _flagGenreMovieAll, ref _flagGenreMovieVertical, 
                                                             ref _flagStudioMovie, ref _flagStudioMovieAll, ref _flagStudioMovieVertical,
                                                             ref _flagAwardMovie, ref _flagAwardMovieAll, ref _flagAwardMovieVertical
                                                             );
                }
              }
            }

            if (_flag1Music && _flag2Music && !Utils.ContainsID(FSelected.WindowsUsingFanartSelectedMusic, nodeValue))
            {
              FSelected.WindowsUsingFanartSelectedMusic.Add(nodeValue, nodeValue);
            }
            if (_flag1ScoreCenter && _flag2ScoreCenter && !Utils.ContainsID(FSelected.WindowsUsingFanartSelectedScoreCenter, nodeValue))
            {
              FSelected.WindowsUsingFanartSelectedScoreCenter.Add(nodeValue, nodeValue);
            }
            if (_flag1Movie && _flag2Movie && !Utils.ContainsID(FSelected.WindowsUsingFanartSelectedMovie, nodeValue))
            {
              FSelected.WindowsUsingFanartSelectedMovie.Add(nodeValue, nodeValue);
            }
            if (_flag1Picture && _flag2Picture && !Utils.ContainsID(FSelected.WindowsUsingFanartSelectedPictures, nodeValue))
            {
              FSelected.WindowsUsingFanartSelectedPictures.Add(nodeValue, nodeValue);
            }
            if (_flagPlay && !Utils.ContainsID(FPlay.WindowsUsingFanartPlay, nodeValue))
            {
              FPlay.WindowsUsingFanartPlay.Add(nodeValue, nodeValue);
            }

            #region ClearArt
            // Play Music ClearArt
            if (_flagClearArtPlay && !Utils.ContainsID(FPlayOther.WindowsUsingFanartPlayClearArt, nodeValue))
            {
              FPlayOther.WindowsUsingFanartPlayClearArt.Add(nodeValue, nodeValue);
            }
            // Selected Music ClearArt
            if (_flagClearArt && !Utils.ContainsID(FSelectedOther.WindowsUsingFanartSelectedClearArtMusic, nodeValue))
            {
              FSelectedOther.WindowsUsingFanartSelectedClearArtMusic.Add(nodeValue, nodeValue);
            }
            #endregion

            #region Genres and Studios
            // Play Music Genre
            if (_flagGenrePlay && !Utils.ContainsID(FPlayOther.WindowsUsingFanartPlayGenre, nodeValue))
            {
              FPlayOther.WindowsUsingFanartPlayGenre.Add(nodeValue, nodeValue);
            }
            if (_flagGenrePlayAll && !Utils.ContainsID(FPlayOther.WindowsUsingFanartPlayGenre, nodeValue + Utils.Logo.Horizontal))
            {
              FPlayOther.WindowsUsingFanartPlayGenre.Add(nodeValue + Utils.Logo.Horizontal, nodeValue + Utils.Logo.Horizontal);
            }
            if (_flagGenrePlayVertical && !Utils.ContainsID(FPlayOther.WindowsUsingFanartPlayGenre, nodeValue + Utils.Logo.Vertical))
            {
              FPlayOther.WindowsUsingFanartPlayGenre.Add(nodeValue + Utils.Logo.Vertical, nodeValue + Utils.Logo.Vertical);
            }
            // Selected Music Genre
            if (_flagGenreMusic && !Utils.ContainsID(FSelectedOther.WindowsUsingFanartSelectedGenreMusic, nodeValue))
            {
              FSelectedOther.WindowsUsingFanartSelectedGenreMusic.Add(nodeValue, nodeValue);
            }
            if (_flagGenreMusicAll && !Utils.ContainsID(FSelectedOther.WindowsUsingFanartSelectedGenreMusic, nodeValue + Utils.Logo.Horizontal))
            {
              FSelectedOther.WindowsUsingFanartSelectedGenreMusic.Add(nodeValue + Utils.Logo.Horizontal, nodeValue + Utils.Logo.Horizontal);
            }
            if (_flagGenreMusicVertical && !Utils.ContainsID(FSelectedOther.WindowsUsingFanartSelectedGenreMusic, nodeValue + Utils.Logo.Vertical))
            {
              FSelectedOther.WindowsUsingFanartSelectedGenreMusic.Add(nodeValue + Utils.Logo.Vertical, nodeValue + Utils.Logo.Vertical);
            }
            // Selected Movie Award
            if (_flagAwardMovie && !Utils.ContainsID(FSelectedOther.WindowsUsingFanartSelectedAwardMovie, nodeValue))
            {
              FSelectedOther.WindowsUsingFanartSelectedAwardMovie.Add(nodeValue, nodeValue);
            }
            if (_flagAwardMovieAll && !Utils.ContainsID(FSelectedOther.WindowsUsingFanartSelectedAwardMovie, nodeValue + Utils.Logo.Horizontal))
            {
              FSelectedOther.WindowsUsingFanartSelectedAwardMovie.Add(nodeValue + Utils.Logo.Horizontal, nodeValue + Utils.Logo.Horizontal);
            }
            if (_flagAwardMovieVertical && !Utils.ContainsID(FSelectedOther.WindowsUsingFanartSelectedAwardMovie, nodeValue + Utils.Logo.Vertical))
            {
              FSelectedOther.WindowsUsingFanartSelectedAwardMovie.Add(nodeValue + Utils.Logo.Vertical, nodeValue + Utils.Logo.Vertical);
            }
            // Selected Movie Genre
            if (_flagGenreMovie && !Utils.ContainsID(FSelectedOther.WindowsUsingFanartSelectedGenreMovie, nodeValue))
            {
              FSelectedOther.WindowsUsingFanartSelectedGenreMovie.Add(nodeValue, nodeValue);
            }
            if (_flagGenreMovieAll && !Utils.ContainsID(FSelectedOther.WindowsUsingFanartSelectedGenreMovie, nodeValue + Utils.Logo.Horizontal))
            {
              FSelectedOther.WindowsUsingFanartSelectedGenreMovie.Add(nodeValue + Utils.Logo.Horizontal, nodeValue + Utils.Logo.Horizontal);
            }
            if (_flagGenreMovieVertical && !Utils.ContainsID(FSelectedOther.WindowsUsingFanartSelectedGenreMovie, nodeValue + Utils.Logo.Vertical))
            {
              FSelectedOther.WindowsUsingFanartSelectedGenreMovie.Add(nodeValue + Utils.Logo.Vertical, nodeValue + Utils.Logo.Vertical);
            }
            // Selected Movie Studio
            if (_flagStudioMovie && !Utils.ContainsID(FSelectedOther.WindowsUsingFanartSelectedStudioMovie, nodeValue))
            {
              FSelectedOther.WindowsUsingFanartSelectedStudioMovie.Add(nodeValue, nodeValue);
            }
            if (_flagStudioMovieAll && !Utils.ContainsID(FSelectedOther.WindowsUsingFanartSelectedStudioMovie, nodeValue + Utils.Logo.Horizontal))
            {
              FSelectedOther.WindowsUsingFanartSelectedStudioMovie.Add(nodeValue + Utils.Logo.Horizontal, nodeValue + Utils.Logo.Horizontal);
            }
            if (_flagStudioMovieVertical && !Utils.ContainsID(FSelectedOther.WindowsUsingFanartSelectedStudioMovie, nodeValue + Utils.Logo.Vertical))
            {
              FSelectedOther.WindowsUsingFanartSelectedStudioMovie.Add(nodeValue + Utils.Logo.Vertical, nodeValue + Utils.Logo.Vertical);
            }
            #endregion

            #region Random
            var skinFile = new FanartRandom.SkinFile();
            xpathNodeIterator = navigator.Select("/window/define");
            if (xpathNodeIterator.Count > 0)
            {
              while (xpathNodeIterator.MoveNext())
              {
                var s = xpathNodeIterator.Current.Value;
                if (s.StartsWith("#useRandomGamesUserFanart", StringComparison.CurrentCulture))
                  skinFile.UseRandomGamesFanartUser = Utils.GetBool(ParseNodeValue(s));
                if (s.StartsWith("#useRandomMoviesUserFanart", StringComparison.CurrentCulture))
                  skinFile.UseRandomMoviesFanartUser = Utils.GetBool(ParseNodeValue(s));
                if (s.StartsWith("#useRandomMoviesScraperFanart", StringComparison.CurrentCulture))
                  skinFile.UseRandomMoviesFanartScraper = Utils.GetBool(ParseNodeValue(s));
                if (s.StartsWith("#useRandomMovingPicturesFanart", StringComparison.CurrentCulture))
                  skinFile.UseRandomMovingPicturesFanart = Utils.GetBool(ParseNodeValue(s));
                if (s.StartsWith("#useRandomMusicUserFanart", StringComparison.CurrentCulture))
                  skinFile.UseRandomMusicFanartUser = Utils.GetBool(ParseNodeValue(s));
                if (s.StartsWith("#useRandomMusicScraperFanart", StringComparison.CurrentCulture))
                  skinFile.UseRandomMusicFanartScraper = Utils.GetBool(ParseNodeValue(s));
                if (s.StartsWith("#useRandomPicturesUserFanart", StringComparison.CurrentCulture))
                  skinFile.UseRandomPicturesFanartUser = Utils.GetBool(ParseNodeValue(s));
                if (s.StartsWith("#useRandomScoreCenterUserFanart", StringComparison.CurrentCulture))
                  skinFile.UseRandomScoreCenterFanartUser = Utils.GetBool(ParseNodeValue(s));
                if (s.StartsWith("#useRandomTVSeriesFanart", StringComparison.CurrentCulture))
                  skinFile.UseRandomTVSeriesFanart = Utils.GetBool(ParseNodeValue(s));
                if (s.StartsWith("#useRandomTVUserFanart", StringComparison.CurrentCulture))
                  skinFile.UseRandomTVFanartUser = Utils.GetBool(ParseNodeValue(s));
                if (s.StartsWith("#useRandomPluginsUserFanart", StringComparison.CurrentCulture))
                  skinFile.UseRandomPluginsFanartUser = Utils.GetBool(ParseNodeValue(s));
                // logger.Debug("*** Random check: " + s + " - " + nodeValue);
              }
            }
            try
            {
              if (skinFile.UseRandomGamesFanartUser || 
                  skinFile.UseRandomMoviesFanartUser || 
                  skinFile.UseRandomMoviesFanartScraper || 
                  skinFile.UseRandomMovingPicturesFanart || 
                  skinFile.UseRandomMusicFanartUser || 
                  skinFile.UseRandomMusicFanartScraper || 
                  skinFile.UseRandomPicturesFanartUser || 
                  skinFile.UseRandomScoreCenterFanartUser || 
                  skinFile.UseRandomTVSeriesFanart || 
                  skinFile.UseRandomTVFanartUser ||
                  skinFile.UseRandomPluginsFanartUser)
              {
                if (Utils.ContainsID(FRandom.WindowsUsingFanartRandom, nodeValue))
                {
                  FRandom.WindowsUsingFanartRandom[nodeValue] = skinFile ; 
                  // logger.Debug("*** Random update: " + nodeValue + " - " + (string.IsNullOrEmpty(ThemeDir) ? string.Empty : "Theme: "+ThemeDir+" ")+" Filename:" + XMLName);
                }
                else
                {
                  FRandom.WindowsUsingFanartRandom.Add(nodeValue, skinFile);
                  // logger.Debug("*** Random add: " + nodeValue + " - " + (string.IsNullOrEmpty(ThemeDir) ? string.Empty : "Theme: "+ThemeDir+" ")+" Filename:" + XMLName);
                }
              }
            }
            catch {  }
            #endregion
          }
        }
        catch (Exception ex)
        {
          logger.Error("SetupWindowsUsingFanartHandlerVisibility: " + (string.IsNullOrEmpty(ThemeDir) ? string.Empty : "Theme: "+ThemeDir+" ")+" Filename:" + XMLName);
          logger.Error(ex) ;
        }
      }

      if (string.IsNullOrEmpty(ThemeDir)) 
      {
        // Include Themes
        if (!string.IsNullOrEmpty(theme))
        {
          SetupWindowsUsingFanartHandlerVisibility(path, theme);
        }
      }
    }

    private void HandleXmlImports(string filename, string windowId, 
                                  ref bool _flag1Music, ref bool _flag2Music, 
                                  ref bool _flag1ScoreCenter, ref bool _flag2ScoreCenter, 
                                  ref bool _flag1Movie, ref bool _flag2Movie,
                                  ref bool _flag1Picture, ref bool _flag2Picture, 
                                  ref bool _flagPlay, 
                                  ref bool _flagClearArt, ref bool _flagClearArtPlay,   
                                  ref bool _flagGenrePlay, ref bool _flagGenrePlayAll, ref bool _flagGenrePlayVertical, 
                                  ref bool _flagGenreMusic, ref bool _flagGenreMusicAll, ref bool _flagGenreMusicVertical, 
                                  ref bool _flagGenreMovie, ref bool _flagGenreMovieAll, ref bool _flagGenreMovieVertical, 
                                  ref bool _flagStudioMovie, ref bool _flagStudioMovieAll, ref bool _flagStudioMovieVertical, 
                                  ref bool _flagAwardMovie, ref bool _flagAwardMovieAll, ref bool _flagAwardMovieVertical
                                  )
    {
      var xpathDocument = new XPathDocument(filename);
      var output = new StringBuilder();
      using (var writer = XmlWriter.Create(output))
      {
        xpathDocument.CreateNavigator().WriteSubtree(writer);
      }
      var _xml = output.ToString();

      #region Play Fanart
      // Play
      if (_xml.Contains("#usePlayFanart:Yes", StringComparison.OrdinalIgnoreCase))
      {
        _flagPlay         = true;
      }
      // Genres
      if (_xml.Contains("#fanarthandler.movie.genres.play") || _xml.Contains("#fanarthandler.music.genres.play"))
      {
        _flagGenrePlay = true;
        if (_xml.Contains("#fanarthandler.movie.genres.play.all") || _xml.Contains("#fanarthandler.music.genres.play.all"))
        {
          _flagGenrePlayAll = true;
        }
        if (_xml.Contains("#fanarthandler.movie.genres.play.verticalall") || _xml.Contains("#fanarthandler.music.genres.play.verticalall"))
        {
          _flagGenrePlayVertical = true;
        }
      }
      // ClearArt
      if (_xml.Contains("#fanarthandler.music.artistclearart.play") || _xml.Contains("#fanarthandler.music.artistbanner.play") || _xml.Contains("#fanarthandler.music.albumcd.play"))
      {
        _flagClearArtPlay = true;
      }
      #endregion

      #region Selected Fanart
      // Selected
      if (_xml.Contains("#useSelectedFanart:Yes", StringComparison.OrdinalIgnoreCase))
      {
        _flag1Music       = true;
        _flag1Movie       = true;
        _flag1Picture     = true;
        _flag1ScoreCenter = true;
      }

      // Backdrop
      if (_xml.Contains("#fanarthandler.music.backdrop1.selected") || _xml.Contains("#fanarthandler.music.backdrop2.selected"))
      {
        _flag2Music       = true;
      }
      if (_xml.Contains("#fanarthandler.movie.backdrop1.selected") || _xml.Contains("#fanarthandler.movie.backdrop2.selected"))
      {
        _flag2Movie       = true;
      }
      if (_xml.Contains("#fanarthandler.picture.backdrop1.selected") || _xml.Contains("#fanarthandler.picture.backdrop2.selected"))
      {
        _flag2Picture     = true;
      }
      if (_xml.Contains("#fanarthandler.scorecenter.backdrop1.selected") || _xml.Contains("#fanarthandler.scorecenter.backdrop2.selected"))
      {
        _flag2ScoreCenter = true;
      }

      // ClearArt
      if (_xml.Contains("#fanarthandler.music.artistclearart.selected") || _xml.Contains("#fanarthandler.music.artistbanner.selected") || _xml.Contains("#fanarthandler.music.albumcd.selected"))
      {
        _flagClearArt = true;
      }

      // Studios
      if (_xml.Contains("#fanarthandler.movie.studios.selected"))
      {
        _flagStudioMovie  = true;
        if (_xml.Contains("#fanarthandler.movie.studios.selected.all"))
        {
          _flagStudioMovieAll = true;
        }
        if (_xml.Contains("#fanarthandler.movie.studios.selected.verticalall"))
        {
          _flagStudioMovieVertical = true;
        }
      }

      // Awards
      if (_xml.Contains("#fanarthandler.movie.awards.selected"))
      {
        _flagAwardMovie  = true;
        if (_xml.Contains("#fanarthandler.movie.awards.selected.all"))
        {
          _flagAwardMovieAll = true;
        }
        if (_xml.Contains("#fanarthandler.movie.awards.selected.verticalall"))
        {
          _flagAwardMovieVertical = true;
        }
      }

      // Genres
      if (_xml.Contains("#fanarthandler.movie.genres.selected") || _xml.Contains("#fanarthandler.music.genres.selected"))
      {
        bool _movies = _xml.Contains("#fanarthandler.movie.genres.selected");
        if (_movies)
        {
          _flagGenreMovie = true;
        }
        else
        {
          _flagGenreMusic = true;
        }
        if (_xml.Contains("#fanarthandler.movie.genres.selected.all") || _xml.Contains("#fanarthandler.music.genres.selected.all"))
        {
          if (_movies)
          {
            _flagGenreMovieAll = true;
          }
          else
          {
            _flagGenreMusicAll = true;
          }
        }
        if (_xml.Contains("#fanarthandler.movie.genres.selected.verticalall") || _xml.Contains("#fanarthandler.music.genres.selected.verticalall"))
        {
          if (_movies)
          {
            _flagGenreMovieVertical = true;
          }
          else
          {
            _flagGenreMusicVertical = true;
          }
        }
      }
      #endregion
    }
    #endregion
  }
}
