﻿// Type: FanartHandler.Utils
// Assembly: FanartHandler, Version=4.0.2.0, Culture=neutral, PublicKeyToken=null
// MVID: 073E8D78-B6AE-4F86-BDE9-3E09A337833B

using MediaPortal.Configuration;
using MediaPortal.GUI.Library;
using MediaPortal.Music.Database;
using MediaPortal.Profile;

using NLog;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

using Monitor.Core.Utilities;
using MediaPortal.TagReader;

namespace FanartHandler
{
  internal static class Utils
  {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private const string ConfigFilename = "FanartHandler.xml";
    private const string ConfigBadArtistsFilename = "FanartHandler.Artists.xml";
    private const string ConfigBadMyPicturesSlideShowFilename = "FanartHandler.SlideShowFolders.xml";
    private const string ConfigGenresFilename = "FanartHandler.Genres.xml";
    private const string ConfigCharactersFilename = "FanartHandler.Characters.xml";
    private const string ConfigStudiosFilename = "FanartHandler.Studios.xml";
    private const string ConfigAwardsFilename = "FanartHandler.Awards.xml";
    private const string FanartHandlerPrefix = "#fanarthandler.";

    private static bool isStopping;
    private static DatabaseManager dbm;

    private static int scrapperTimerInterval = 3600000; // milliseconds
    private static int refreshTimerInterval = 250; // milliseconds
    private static int maxRefreshTickCount = 120;  // 30sec - 120 / (1000 / 250)
    private static int idleTimeInMillis = 250;

    private const int ThreadSleep = 0;
    private const int ThreadLongSleep = 500;

    private static double MinWResolution;
    private static double MinHResolution;

    private static Hashtable defaultBackdropImages;
    private static Hashtable slideshowImages;

    private static int activeWindow = (int)GUIWindow.Window.WINDOW_INVALID;

    private static bool AddAwardsToGenre = false;

    public static DateTime LastRefreshRecording { get; set; }
    public static bool Used4TRTV { get; set; }
    public static Hashtable DelayStop { get; set; }

    public static List<string> BadArtistsList;  
    public static List<string> MyPicturesSlideShowFolders;  
    public static string[] PipesArray ;
    public static Hashtable Genres;
    public static Hashtable Characters;
    public static Hashtable Studios;
    public static List<KeyValuePair<string, object>> AwardsList;

    #region Settings
    public static bool UseFanart { get; set; }
    public static bool UseAlbum { get; set; } 
    public static bool UseArtist { get; set; } 
    public static bool SkipWhenHighResAvailable { get; set; } 
    public static bool DisableMPTumbsForRandom { get; set; } 
    public static string ImageInterval { get; set; } 
    public static string MinResolution { get; set; } 
    public static string ScraperMaxImages { get; set; } 
    public static bool ScraperMusicPlaying { get; set; } 
    public static bool ScraperMPDatabase { get; set; } 
    public static string ScraperInterval { get; set; } 
    public static bool UseAspectRatio { get; set; } 
    public static bool ScrapeThumbnails { get; set; } 
    public static bool ScrapeThumbnailsAlbum { get; set; } 
    public static bool DoNotReplaceExistingThumbs { get; set; } 
    public static bool UseGenreFanart { get; set; } 
    public static bool ScanMusicFoldersForFanart { get; set; } 
    public static string MusicFoldersArtistAlbumRegex { get; set; } 
    public static bool UseOverlayFanart { get; set; } 
    public static bool UseMusicFanart { get; set; } 
    public static bool UseVideoFanart { get; set; } 
    public static bool UsePicturesFanart { get; set; } 
    public static bool UseScoreCenterFanart { get; set; } 
    public static string DefaultBackdrop { get; set; } 
    public static string DefaultBackdropMask { get; set; } 
    public static bool DefaultBackdropIsImage { get; set; } 
    public static bool UseDefaultBackdrop { get; set; } 
    public static bool UseSelectedMusicFanart { get; set; } 
    public static bool UseSelectedOtherFanart { get; set; } 
    public static string FanartTVPersonalAPIKey { get; set; }
    public static bool DeleteMissing { get; set; }
    public static bool UseHighDefThumbnails { get; set; }
    public static bool UseMinimumResolutionForDownload { get; set; }
    public static bool ShowDummyItems { get; set; }
    public static bool AddAdditionalSeparators { get; set; }
    public static bool UseMyPicturesSlideShow { get; set; }
    public static bool FastScanMyPicturesSlideShow { get; set; }
    public static int LimitNumberFanart { get; set; }
    #endregion

    #region Providers
    public static bool UseFanartTV { get; set; }
    public static bool UseHtBackdrops { get; set; }
    public static bool UseLastFM { get; set; }
    public static bool UseCoverArtArchive { get; set; }
    #endregion

    #region Fanart.TV 
    public static bool MusicClearArtDownload { get; set; }
    public static bool MusicBannerDownload { get; set; }
    public static bool MusicCDArtDownload { get; set; }
    public static bool MoviesClearArtDownload { get; set; }
    public static bool MoviesBannerDownload { get; set; }
    public static bool MoviesClearLogoDownload { get; set; }
    public static bool MoviesCDArtDownload { get; set; }
    public static bool MoviesFanartNameAsMediaportal { get; set; }  // movieid{0..9} instead movieid{FanartTVImageID}
    public static string FanartTVLanguage { get; set; }
    public static string FanartTVLanguageDef { get; set; }
    public static bool FanartTVLanguageToAny { get; set; }
    #endregion

    public static bool WatchFullThumbFolder { get; set; }

    #region FanartHandler folders
    public static string MPThumbsFolder { get; set; }
    public static string FAHFolder { get; set; }
    public static string FAHUDFolder { get; set; }
    public static string FAHUDGames { get; set; }
    public static string FAHUDMovies { get; set; }
    public static string FAHUDMusic { get; set; }
    public static string FAHUDMusicAlbum { get; set; }
    // public static string FAHUDMusicGenre { get; set; }
    public static string FAHUDPictures { get; set; }
    public static string FAHUDScorecenter { get; set; }
    public static string FAHUDTV { get; set; }
    public static string FAHUDPlugins { get; set; }

    public static string FAHSFolder { get; set; }
    public static string FAHSMovies { get; set; }
    public static string FAHSMusic { get; set; }

    public static string FAHMusicArtists { get; set; }
    public static string FAHMusicAlbums { get; set; }

    public static string FAHTVSeries { get; set; }
    public static string FAHMovingPictures { get; set; }
    public static string FAHWatchFolder { get; set; }

    public static string FAHMVCArtists { get; set; }
    public static string FAHMVCAlbums { get; set; }
    #endregion

    #region Fanart.TV folders
    public static string MusicClearArtFolder { get; set; }
    public static string MusicBannerFolder { get; set; }
    public static string MusicCDArtFolder { get; set; }
    public static string MusicMask { get; set; }
    public static string MoviesClearArtFolder { get; set; }
    public static string MoviesBannerFolder { get; set; }
    public static string MoviesClearLogoFolder { get; set; }
    public static string MoviesCDArtFolder { get; set; }
    #endregion

    #region Genres, Awards and Studios folders
    public static string FAHGenres { get; set; }
    public static string FAHCharacters { get; set; }
    public static string FAHStudios { get; set; }
    public static string FAHAwards { get; set; }
    #endregion

    #region Junction
    public static bool IsJunction { get; set; }
    public static string JunctionSource { get; set; }
    public static string JunctionTarget { get; set; }
    #endregion

    public static int iActiveWindow
    {
      get { return activeWindow; }
      set { activeWindow = value; }
    }

    public static string sActiveWindow
    {
      get { return activeWindow.ToString(); }
    }

    public static int IdleTimeInMillis
    {
      get { return idleTimeInMillis; }
      set { idleTimeInMillis = value; }
    }

    internal static int ScrapperTimerInterval
    {
      get { return scrapperTimerInterval; }
      set { scrapperTimerInterval = value; }
    }

    internal static int RefreshTimerInterval
    {
      get { return refreshTimerInterval; }
      set { refreshTimerInterval = value; }
    }

    internal static int MaxRefreshTickCount
    {
      get { return maxRefreshTickCount; }
      set { maxRefreshTickCount = value; }
    }

    public static Hashtable DefaultBackdropImages
    {
      get { return defaultBackdropImages; }
      set { defaultBackdropImages = value; }
    }

    public static Hashtable SlideShowImages
    {
      get { return slideshowImages; }
      set { slideshowImages = value; }
    }

    public static double CurrArtistsBeingScraped 
    { 
      get { return dbm.CurrArtistsBeingScraped; }
      set { dbm.CurrArtistsBeingScraped = value; }
    }
    public static double TotArtistsBeingScraped 
    {
      get { return dbm.TotArtistsBeingScraped; }
      set { dbm.TotArtistsBeingScraped = value; }
    }

    public static bool IsScraping 
    {
      get { return dbm.IsScraping; }
      set { dbm.IsScraping = value; }
    }

    public static bool StopScraper 
    {
      get { return dbm.StopScraper; }
      set { dbm.StopScraper = value; }
    }

    static Utils()
    {
    }

    #region Fanart Handler folders initialize
    public static void InitFolders()
    {
      logger.Info("Fanart Handler folder initialize starting.");

      #region Empty.Fill
      MusicClearArtFolder = string.Empty;
      MusicBannerFolder = string.Empty;
      MusicCDArtFolder = string.Empty;
      MusicMask = "{0} - {1}"; // MePoTools
      MoviesClearArtFolder = string.Empty;
      MoviesBannerFolder = string.Empty;
      MoviesCDArtFolder = string.Empty;
      MoviesClearLogoFolder= string.Empty;

      FAHFolder = string.Empty;
      FAHUDFolder = string.Empty;
      FAHUDGames = string.Empty;
      FAHUDMovies = string.Empty;
      FAHUDMusic = string.Empty;
      FAHUDMusicAlbum = string.Empty;
      // FAHUDMusicGenre = string.Empty;
      FAHUDPictures = string.Empty;
      FAHUDScorecenter = string.Empty;
      FAHUDTV = string.Empty;
      FAHUDPlugins = string.Empty;

      FAHSFolder = string.Empty;
      FAHSMovies = string.Empty;
      FAHSMusic = string.Empty;

      FAHMusicArtists = string.Empty;
      FAHMusicAlbums = string.Empty;

      FAHTVSeries = string.Empty;
      FAHMovingPictures = string.Empty;

      FAHWatchFolder = string.Empty;

      IsJunction = false;
      JunctionSource = string.Empty;
      JunctionTarget = string.Empty;

      FAHGenres = string.Empty;
      FAHCharacters = string.Empty;
      FAHStudios = string.Empty;
      FAHAwards = string.Empty;
      #endregion

      MPThumbsFolder = Config.GetFolder((Config.Dir) 6) ;
      logger.Debug("Mediaportal Thumb folder: "+MPThumbsFolder);

      #region Fill.MusicFanartFolders
      MusicClearArtFolder = Path.Combine(MPThumbsFolder, @"ClearArt\Music\"); // MePotools
      if (!Directory.Exists(MusicClearArtFolder) || IsDirectoryEmpty(MusicClearArtFolder))
      {
        MusicClearArtFolder = Path.Combine(MPThumbsFolder, @"Music\ClearArt\"); // MusicInfo Handler
        if (!Directory.Exists(MusicClearArtFolder) || IsDirectoryEmpty(MusicClearArtFolder))
        {
          MusicClearArtFolder = Path.Combine(MPThumbsFolder, @"Music\ClearLogo\FullSize\"); // DVDArt
          if (!Directory.Exists(MusicClearArtFolder) || IsDirectoryEmpty(MusicClearArtFolder))
            MusicClearArtFolder = string.Empty;
        }
      }
      logger.Debug("Fanart Handler Music ClearArt folder: "+MusicClearArtFolder);

      MusicBannerFolder = Path.Combine(MPThumbsFolder, @"Banner\Music\"); // MePotools
      if (!Directory.Exists(MusicBannerFolder) || IsDirectoryEmpty(MusicBannerFolder))
      {
        MusicBannerFolder = Path.Combine(MPThumbsFolder, @"Music\Banner\FullSize\"); // DVDArt
        if (!Directory.Exists(MusicBannerFolder) || IsDirectoryEmpty(MusicBannerFolder))
          MusicBannerFolder = string.Empty;
      }
      logger.Debug("Fanart Handler Music Banner folder: "+MusicBannerFolder);

      MusicCDArtFolder = Path.Combine(MPThumbsFolder, @"CDArt\Music\"); // MePotools
      if (!Directory.Exists(MusicCDArtFolder) || IsDirectoryEmpty(MusicCDArtFolder))
      {
        MusicCDArtFolder = Path.Combine(MPThumbsFolder, @"Music\cdArt\"); // MusicInfo Handler
        if (!Directory.Exists(MusicCDArtFolder) || IsDirectoryEmpty(MusicCDArtFolder))
        {
          MusicCDArtFolder = Path.Combine(MPThumbsFolder, @"Music\CDArt\FullSize\"); // DVDArt
          if (!Directory.Exists(MusicCDArtFolder) || IsDirectoryEmpty(MusicCDArtFolder))
            MusicCDArtFolder = string.Empty;
        }
        MusicMask = "{0}-{1}"; // Mediaportal
      }
      logger.Debug("Fanart Handler Music CD folder: "+MusicCDArtFolder+" Mask: "+MusicMask);

      MoviesClearArtFolder = Path.Combine(MPThumbsFolder, @"ClearArt\Movies\"); // MePotools
      if (!Directory.Exists(MoviesClearArtFolder) || IsDirectoryEmpty(MoviesClearArtFolder))
      {
        MoviesClearArtFolder = Path.Combine(MPThumbsFolder, @"Movies\ClearArt\FullSize\"); // DVDArt
        if (!Directory.Exists(MoviesClearArtFolder) || IsDirectoryEmpty(MoviesClearArtFolder))
          MoviesClearArtFolder = string.Empty;
      }
      logger.Debug("Fanart Handler Movies ClearArt folder: "+MoviesClearArtFolder);

      MoviesBannerFolder = Path.Combine(MPThumbsFolder, @"Banner\Movies\"); // MePotools
      if (!Directory.Exists(MoviesBannerFolder) || IsDirectoryEmpty(MoviesBannerFolder))
      {
        MoviesBannerFolder = Path.Combine(MPThumbsFolder, @"Movies\Banner\FullSize\"); // DVDArt
        if (!Directory.Exists(MoviesBannerFolder) || IsDirectoryEmpty(MoviesBannerFolder))
          MoviesBannerFolder = string.Empty;
      }
      logger.Debug("Fanart Handler Movies Banner folder: "+MoviesBannerFolder);

      MoviesCDArtFolder = Path.Combine(MPThumbsFolder, @"CDArt\Movies\"); // MePotools
      if (!Directory.Exists(MoviesCDArtFolder) || IsDirectoryEmpty(MoviesCDArtFolder))
      {
        MoviesCDArtFolder = Path.Combine(MPThumbsFolder, @"Movies\DVDArt\FullSize\"); // DVDArt
        if (!Directory.Exists(MoviesCDArtFolder) || IsDirectoryEmpty(MoviesCDArtFolder))
          MoviesCDArtFolder = string.Empty;
      }
      logger.Debug("Fanart Handler Movies CD folder: "+MoviesCDArtFolder);

      MoviesClearLogoFolder = Path.Combine(MPThumbsFolder, @"ClearLogo\Movies\"); // MePotools
      if (!Directory.Exists(MoviesClearLogoFolder) || IsDirectoryEmpty(MoviesClearLogoFolder))
      {
        MoviesClearLogoFolder = Path.Combine(MPThumbsFolder, @"Movies\ClearLogo\FullSize\"); // DVDArt
        if (!Directory.Exists(MoviesClearLogoFolder) || IsDirectoryEmpty(MoviesClearLogoFolder))
          MoviesClearLogoFolder = string.Empty;
      }
      logger.Debug("Fanart Handler Movies ClearLogo folder: "+MoviesClearLogoFolder);
      #endregion

      #region Fill.FanartHandler 
      FAHFolder = Path.Combine(MPThumbsFolder, @"Skin FanArt\");
      logger.Debug("Fanart Handler root folder: "+FAHFolder);

      FAHUDFolder = Path.Combine(FAHFolder, @"UserDef\");
      logger.Debug("Fanart Handler User folder: "+FAHUDFolder);
      FAHUDGames = Path.Combine(FAHUDFolder, @"games\");
      logger.Debug("Fanart Handler User Games folder: "+FAHUDGames);
      FAHUDMovies = Path.Combine(FAHUDFolder, @"movies\");
      logger.Debug("Fanart Handler User Movies folder: "+FAHUDMovies);
      FAHUDMusic = Path.Combine(FAHUDFolder, @"music\");
      logger.Debug("Fanart Handler User Music folder: "+FAHUDMusic);
      FAHUDMusicAlbum = Path.Combine(FAHUDFolder, @"albums\");
      logger.Debug("Fanart Handler User Music Album folder: "+FAHUDMusicAlbum);
      // FAHUDMusicGenre = Path.Combine(FAHUDFolder, @"Scraper\Genres\");
      // logger.Debug("Fanart Handler User Music Genre folder: "+FAHUDMusicGenre);
      FAHUDPictures = Path.Combine(FAHUDFolder, @"pictures\");
      logger.Debug("Fanart Handler User Pictures folder: "+FAHUDPictures);
      FAHUDScorecenter = Path.Combine(FAHUDFolder, @"scorecenter\");
      logger.Debug("Fanart Handler User Scorecenter folder: "+FAHUDScorecenter);
      FAHUDTV = Path.Combine(FAHUDFolder, @"tv\");
      logger.Debug("Fanart Handler User TV folder: "+FAHUDTV);
      FAHUDPlugins = Path.Combine(FAHUDFolder, @"plugins\");
      logger.Debug("Fanart Handler User Plugins folder: "+FAHUDPlugins);

      FAHSFolder = Path.Combine(FAHFolder, @"Scraper\"); 
      logger.Debug("Fanart Handler Scraper folder: "+FAHSFolder);
      FAHSMovies = Path.Combine(FAHSFolder, @"movies\"); 
      logger.Debug("Fanart Handler Scraper Movies folder: "+FAHSMovies);
      FAHSMusic = Path.Combine(FAHSFolder, @"music\"); 
      logger.Debug("Fanart Handler Scraper Music folder: "+FAHSMusic);

      FAHMusicArtists = Path.Combine(MPThumbsFolder, @"Music\Artists\");
      logger.Debug("Mediaportal Artists thumbs folder: "+FAHMusicArtists);
      FAHMusicAlbums = Path.Combine(MPThumbsFolder, @"Music\Albums\");
      logger.Debug("Mediaportal Albums thumbs folder: "+FAHMusicAlbums);

      FAHTVSeries = Path.Combine(MPThumbsFolder, @"Fan Art\fanart\original\");
      logger.Debug("TV-Series Fanart folder: "+FAHTVSeries);
      FAHMovingPictures = Path.Combine(MPThumbsFolder, @"MovingPictures\Backdrops\FullSize\");
      logger.Debug("MovingPictures Fanart folder: "+FAHMovingPictures);

      FAHMVCArtists = Path.Combine(MPThumbsFolder, @"mvCentral\Artists\FullSize\");
      logger.Debug("mvCentral Artists folder: "+FAHTVSeries);
      FAHMVCAlbums = Path.Combine(MPThumbsFolder, @"mvCentral\Albums\FullSize\");
      logger.Debug("mvCentral Albums folder: "+FAHTVSeries);
      #endregion

      #region Genres and Studios folders
      FAHGenres = @"\Media\Logos\Genres\";
      logger.Debug("Fanart Handler Genres folder: Theme|Skin|Thumb "+FAHGenres);
      FAHCharacters = FAHGenres + @"Characters\";
      logger.Debug("Fanart Handler Characters folder: Theme|Skin|Thumb "+FAHCharacters);
      FAHStudios = @"\Media\Logos\Studios\";
      logger.Debug("Fanart Handler Studios folder: Theme|Skin|Thumb "+FAHStudios);
      FAHAwards = @"\Media\Logos\Awards\";
      logger.Debug("Fanart Handler Awards folder: Theme|Skin|Thumb "+FAHAwards);
      #endregion

      WatchFullThumbFolder = true ;
      
      #region Junction
      if (WatchFullThumbFolder)
      {
        // Check MP Thumbs folder for Junction
        try
        {
          IsJunction = JunctionPoint.Exists(MPThumbsFolder);
          if (IsJunction)
          {
            JunctionSource = MPThumbsFolder;
            JunctionTarget = JunctionPoint.GetTarget(JunctionSource).Trim().Replace(@"UNC\", @"\\");
            FAHWatchFolder = JunctionTarget;
            logger.Debug("Junction detected: "+JunctionSource+" -> "+JunctionTarget);
          }
          else
            FAHWatchFolder = MPThumbsFolder;
        }
        catch
        {
          FAHWatchFolder = MPThumbsFolder;
        }
      }
      else // Watch Only FA folders ...
      {
        var iIsJunction = false ;
        // Check MP Thumbs folder for Junction
        try
        {
          iIsJunction = JunctionPoint.Exists(MPThumbsFolder);
          if (iIsJunction)
          {
            JunctionSource = MPThumbsFolder;
            JunctionTarget = JunctionPoint.GetTarget(JunctionSource).Trim().Replace(@"UNC\", @"\\");
            FAHWatchFolder = Path.Combine(JunctionTarget, @"Skin FanArt\");
            logger.Debug("Junction detected: "+JunctionSource+" -> "+JunctionTarget);
            IsJunction = iIsJunction;
          }
          else
            FAHWatchFolder = FAHFolder;
        }
        catch
        {
          FAHWatchFolder = FAHFolder;
        }
        // Check Fanart Handler Fanart folder for Junction
        try
        {
          iIsJunction = JunctionPoint.Exists(FAHWatchFolder);
          if (iIsJunction)
          {
            JunctionSource = FAHWatchFolder;
            JunctionTarget = JunctionPoint.GetTarget(JunctionSource).Trim().Replace(@"UNC\", @"\\");
            FAHWatchFolder = JunctionTarget ;
            logger.Debug("Junction detected: "+Utils.JunctionSource+" -> "+Utils.JunctionTarget);
            IsJunction = iIsJunction;
          }
        }
        catch { }
      }
      logger.Debug("Fanart Handler file watcher folder: "+FAHWatchFolder);
      #endregion

      logger.Info("Fanart Handler folder initialize done.");
    }
    #endregion

    #region Music Fanart in Music folders
    public static void ScanMusicFoldersForFanarts()
    {
      logger.Info("Refreshing local fanart for Music (Music folder Artist/Album Fanart) is starting.");
      int MaximumShares = 250;
      using (var xmlreader = new Settings(Config.GetFile((Config.Dir) 10, "MediaPortal.xml")))
      {
        for (int index = 0; index < MaximumShares; index++)
        {
          string sharePath = String.Format("sharepath{0}", index);
          string sharePin = String.Format("pincode{0}", index);
          string sharePathData = xmlreader.GetValueAsString("music", sharePath, string.Empty);
          string sharePinData = xmlreader.GetValueAsString("music", sharePin, string.Empty);
          if (!MediaPortal.Util.Utils.IsDVD(sharePathData) && sharePathData != string.Empty && string.IsNullOrEmpty(sharePinData))
          {
            logger.Debug("Mediaportal Music folder: "+sharePathData) ;
            SetupFilenames(sharePathData, "fanart*.jpg", Utils.Category.MusicFanartManual, null, Utils.Provider.MusicFolder, true);
          }
        }
      }
      logger.Info("Refreshing local fanart for Music (Music folder Artist/Album fanart) is done.");
    }
    #endregion

    public static DatabaseManager GetDbm()
    {
      return dbm;
    }

    public static void InitiateDbm(string type)
    {
      dbm = new DatabaseManager();
      dbm.InitDB(type);
    }

    public static void WaitForDB()
    {
      if (!dbm.isDBInit)
      {
        logger.Debug("Wait for DB...");
      }
      while (!dbm.isDBInit)
      {
        ThreadToLongSleep();
      }
    }

    public static void ThreadToSleep()
    {
      Thread.Sleep(Utils.ThreadSleep); 
      // Application.DoEvents();
    }

    public static void ThreadToLongSleep()
    {
      Thread.Sleep(Utils.ThreadLongSleep); 
    }

    public static void AllocateDelayStop(string key)
    {
      if (string.IsNullOrEmpty(key))
        return ;

      if (DelayStop == null)
      {
        DelayStop = new Hashtable();
      }
      if (DelayStop.Contains(key))
      {
        DelayStop[key] = (int)DelayStop[key] + 1;
      }
      else
      {
        DelayStop.Add(key, 1);
      }
    }

    public static bool GetDelayStop()
    {
      if ((DelayStop == null) || (DelayStop.Count <= 0))
        return false;

      int i = 0;
      foreach (DictionaryEntry de in DelayStop)
      {
        i++;
        logger.Debug("DelayStop (" + i + "):" + de.Key.ToString() + " [" + de.Value.ToString() + "]");
      }
      return true;
    }

    public static void ReleaseDelayStop(string key)
    {
      if ((DelayStop == null) || (DelayStop.Count <= 0) || string.IsNullOrEmpty(key))
        return;

      if (DelayStop.Contains(key))
      {
        DelayStop[key] = (int)DelayStop[key] - 1;
        if ((int)DelayStop[key] <= 0)
        {
          DelayStop.Remove(key);
        }
      }
    }

    public static void SetIsStopping(bool b)
    {
      isStopping = b;
    }

    public static bool GetIsStopping()
    {
      return isStopping;
    }

    public static string GetMusicFanartCategoriesInStatement(bool highDef)
    {
      if (highDef)
        return "'" + ((object) Category.MusicFanartManual).ToString() + "','" + ((object) Category.MusicFanartScraped).ToString() + "'";
      else
        return "'" + (object) ((object) Category.MusicFanartManual).ToString() + "','" + ((object) Category.MusicFanartScraped).ToString() + "','" + Category.MusicArtistThumbScraped + "','" + Category.MusicAlbumThumbScraped + "'";
    }

    public static string GetMusicAlbumCategoriesInStatement()
    {
      return "'" + ((object) Category.MusicAlbumThumbScraped).ToString() + "'";
    }

    public static string GetMusicArtistCategoriesInStatement()
    {
      return "'" + ((object) Category.MusicArtistThumbScraped).ToString() + "'";
    }

    public static string Equalize(this string self)
    {
      if (string.IsNullOrEmpty(self))
        return string.Empty;

      var key = self.ToLowerInvariant().Trim();
      key = Utils.RemoveDiacritics(key).Trim();
      key = Regex.Replace(key, @"[^\w|;&]", " ");
      key = Regex.Replace(key, @"\b(and|und|en|et|y|и)\b", " & ");
      key = Regex.Replace(key, @"\si(\b)", " 1$1");
      key = Regex.Replace(key, @"\sii(\b)", " 2$1");
      key = Regex.Replace(key, @"\siii(\b)", " 3$1");
      key = Regex.Replace(key, @"\siv(\b)", " 4$1");    
      key = Regex.Replace(key, @"\sv(\b)", " 5$1");       
      key = Regex.Replace(key, @"\svi(\b)", " 6$1");        
      key = Regex.Replace(key, @"\svii(\b)", " 7$1");         
      key = Regex.Replace(key, @"\sviii(\b)", " 8$1");          
      key = Regex.Replace(key, @"\six(\b)", " 9$1");
      key = Regex.Replace(key, @"\s(1)$", string.Empty);
      key = Regex.Replace(key, @"[^\w|;&]", " ");                     
      key = Utils.TrimWhiteSpace(key);
      return key;
    }

    public static string RemoveSpacesAndDashs(this string self)
    {
      if (self == null)
        return string.Empty;

      return self.Replace(" ", "").Replace("-", "").Trim();
    }

    public static string RemoveDiacritics(this string self)
    {
      if (self == null)
        return string.Empty;

      var str = self.Normalize(NormalizationForm.FormD);
      var stringBuilder = new StringBuilder();
      var index = 0;
      while (index < str.Length )
      {
        if (CharUnicodeInfo.GetUnicodeCategory(str[index]) != UnicodeCategory.NonSpacingMark)
          stringBuilder.Append(str[index]);
        checked { ++index; }
      }
      // logger.Debug("*** "+self+" - " + stringBuilder.ToString() + " - " + stringBuilder.ToString().Normalize(NormalizationForm.FormC)) ;
      return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string ReplaceDiacritics(this string self)
    {
      if (self == null)
        return string.Empty;
      var str1 = self;
      var str2 = self.RemoveDiacritics();
      var stringBuilder = new StringBuilder();
      var index = 0;
      while (index < str1.Length)
      {
        if (!str1[index].Equals(str2[index]))
          stringBuilder.Append("*");
        else
          stringBuilder.Append(str1[index]);
        checked { ++index; }
      }
      return stringBuilder.ToString();
    }

    public static bool IsMatch(string s1, string s2, ArrayList al)
    {
      if (s1 == null || s2 == null)
        return false;
      if (IsMatch(s1, s2))
        return true;
      if (al != null)
      {
        var index = 0;
        while (index < al.Count)
        {
          s2 = al[index].ToString().Trim();
          s2 = GetArtist(s2, Category.MusicFanartScraped);
          if (IsMatch(s1, s2))
            return true;
          checked { ++index; }
        }
      }
      return false;
    }

    public static bool IsMatch(string s1, string s2)
    {
      if (s1 == null || s2 == null)
        return false;
      var num = 0;
      if (s1.Length > s2.Length)
        num = checked (s1.Length - s2.Length);
      else if (s2.Length > s1.Length)
        num = checked (s1.Length - s2.Length);
      if (IsInteger(s1))
      {
        return s2.Contains(s1) && num <= 2;
      }
      else
      {
        s2 = RemoveTrailingDigits(s2);
        s1 = RemoveTrailingDigits(s1);
        return s2.Equals(s1, StringComparison.CurrentCulture);
      }
    }

    public static bool IsInteger(string theValue)
    {
      if (string.IsNullOrEmpty(theValue))
        return false;
      else
        return new Regex(@"^\d+$").Match(theValue).Success;
    }

    public static string TrimWhiteSpace(this string self)
    {
      if (string.IsNullOrEmpty(self))
        return string.Empty;
      else
        return Regex.Replace(self, @"\s{2,}", " ").Trim();
    }

    public static string RemoveSpecialChars(string key)
    {
      if (string.IsNullOrEmpty(key))
        return string.Empty;
      // key = Regex.Replace(key, "_", string.Empty); 
      // key = Regex.Replace(key, ":", string.Empty);
      // key = Regex.Replace(key, ";", string.Empty);
      key = Regex.Replace(key.Trim(), "[_;:]", " ");
      return key;
    }

    public static string RemoveMinusFromArtistName (string key)
    {
      if (string.IsNullOrEmpty(key))
        return string.Empty;

      for (int index = 0; index < BadArtistsList.Count; index++)
      {
        string ArtistData = BadArtistsList[index];
        var Left  = ArtistData.Substring(0, ArtistData.IndexOf("|"));
        var Right = ArtistData.Substring(checked (ArtistData.IndexOf("|") + 1));
        key = key.ToLower().Replace(Left, Right) ;
      }
      return key;
    }

    public static string PrepareArtistAlbum (string key, Category category)
    {
      if (string.IsNullOrEmpty(key))
        return string.Empty;

      key = GetFileName(key);
      key = RemoveExtension(key);
      if (category == Category.TvSeriesScraped)
        return key;
      key = Regex.Replace(key, @"\(\d{5}\)", string.Empty).Trim();
      if ((category == Category.MusicArtistThumbScraped) || (category == Category.MusicAlbumThumbScraped))
        key = Regex.Replace(key, "[L]$", string.Empty).Trim();
      key = Regex.Replace(key, @"(\(|{)([0-9]+)(\)|})$", string.Empty).Trim();
      key = RemoveResolutionFromFileName(key) ;
      key = RemoveSpecialChars(key);
      key = RemoveMinusFromArtistName(key);
      return key;
    }

    public static string GetArtist(string key, Category category)
    {
      if (string.IsNullOrEmpty(key))
        return string.Empty;

      key = PrepareArtistAlbum(key, category);
      if ((category == Category.MusicAlbumThumbScraped || category == Category.MusicFanartAlbum) && key.IndexOf("-", StringComparison.CurrentCulture) > 0)
        key = key.Substring(0, key.IndexOf("-", StringComparison.CurrentCulture));
      if (category == Category.TvSeriesScraped)  // [SeriesID]S[Season]*.jpg
      { 
        if (key.IndexOf("S", StringComparison.CurrentCulture) > 0)
          key = key.Substring(0, key.IndexOf("S", StringComparison.CurrentCulture)).Trim();
        if (key.IndexOf("-", StringComparison.CurrentCulture) > 0)
          key = key.Substring(0, key.IndexOf("-", StringComparison.CurrentCulture)).Trim();
      }
      else
        key = Utils.Equalize(key);
      key = Utils.MovePrefixToFront(key);
      //
      return key;
    }

    public static string GetAlbum(string key, Category category)
    {
      if (string.IsNullOrEmpty(key))
        return string.Empty;

      key = PrepareArtistAlbum(key, category);
      if ((category == Category.MusicAlbumThumbScraped || category == Category.MusicFanartAlbum) && key.IndexOf("-", StringComparison.CurrentCulture) > 0)
        key = key.Substring(checked (key.IndexOf("-", StringComparison.CurrentCulture) + 1));
      if ((category != Category.MovieScraped) && 
          (category != Category.MusicArtistThumbScraped) && 
          (category != Category.MusicAlbumThumbScraped) && 
          (category != Category.MusicFanartManual) && 
          (category != Category.MusicFanartScraped) &&
          (category != Category.MusicFanartAlbum) 
         )
        key = RemoveTrailingDigits(key);
      if (category == Category.TvSeriesScraped) // [SeriesID]S[Season]*.jpg
      {
        if (key.IndexOf("S", StringComparison.CurrentCulture) > 0)
          key = key.Substring(checked (key.IndexOf("S", StringComparison.CurrentCulture) + 1)).Trim();
        if (key.IndexOf("-", StringComparison.CurrentCulture) > 0)
          key = key.Substring(0, key.IndexOf("-", StringComparison.CurrentCulture)).Trim();
      }
      else
        key = Utils.Equalize(key);
      key = Utils.MovePrefixToFront(key);
      return key;
    }

    public static string GetArtistAlbumFromFolder(string FileName, string ArtistAlbumRegex, string groupname)
    {
      var Result = (string) null ;         

      if (string.IsNullOrEmpty(FileName) || string.IsNullOrEmpty(ArtistAlbumRegex) || string.IsNullOrEmpty(groupname))
        return Result ;

      Regex ru = new Regex(ArtistAlbumRegex.Trim(),RegexOptions.IgnoreCase);
      MatchCollection mcu = ru.Matches(FileName.Trim()) ;
      foreach(Match mu in mcu)
      {
        Result = mu.Groups[groupname].Value.ToString();
        if (!string.IsNullOrEmpty(Result))
          break;
      }
      // logger.Debug("*** "+groupname+" "+ArtistAlbumRegex+" "+FileName+" - "+Result);
      return Result ;
    } 

    public static string GetArtistFromFolder(string FileName, string ArtistAlbumRegex) 
    {
      if (string.IsNullOrEmpty(FileName))
        return string.Empty;
      if (string.IsNullOrEmpty(ArtistAlbumRegex))
        return string.Empty;
      if (ArtistAlbumRegex.IndexOf("?<artist>") < 0)
        return string.Empty;

      return GetArtistAlbumFromFolder(FileName, ArtistAlbumRegex, "artist") ;
    }

    public static string GetAlbumFromFolder(string FileName, string ArtistAlbumRegex) 
    {
      if (string.IsNullOrEmpty(FileName))
        return string.Empty;
      if (string.IsNullOrEmpty(ArtistAlbumRegex))
        return string.Empty;
      if (ArtistAlbumRegex.IndexOf("?<album>") < 0)
        return string.Empty;

      return GetArtistAlbumFromFolder(FileName, ArtistAlbumRegex, "album") ;
    }

    public static string HandleMultipleArtistNamesForDBQuery(string inputName)
    {
      if (string.IsNullOrEmpty(inputName))
        return string.Empty;

      var artists = "'" + inputName.Trim() + "'";
      var strArray = inputName.ToLower().
                     //  Replace(";", "|").
                     //  Replace(" ft ", "|").
                     //  Replace(" feat ", "|").
                     //  Replace(" and ", "|").
                     //  Replace(" & ", "|").
                     //  Replace(" и ", "|").
                     //  Replace(",", "|").
                     Trim().
                     Split(Utils.PipesArray, StringSplitOptions.RemoveEmptyEntries);

      foreach (var artist in strArray)
      {
        if (!string.IsNullOrEmpty(artist))
          artists = artists + "," + "'" + artist.Trim() + "'";
      }
      return artists;
    }

    public static string RemoveMPArtistPipes(string s) // ajs: WTF? That this procedure does? And why should she?
    {
      if (s == null)
        return string.Empty;
      else
        // ajs: WAS: return s;
        return RemoveMPArtistPipe(s) ;
    }

    public static string RemoveMPArtistPipe(string s)
    {
      if (s == null)
        return string.Empty;
      // s = s.Replace("|", string.Empty);
      s = s.Replace("|", " ").Replace(";", " ");
      s = s.Trim();
      return s;
    }

    public static ArrayList GetMusicVideoArtists(string dbName)
    {
      var externalDatabaseManager1 = (ExternalDatabaseManager) null;
      var arrayList = new ArrayList();
      
      try
      {
        externalDatabaseManager1 = new ExternalDatabaseManager();
        var str = string.Empty;
        if (externalDatabaseManager1.InitDB(dbName))
        {
          var data = externalDatabaseManager1.GetData(Category.MusicFanartScraped);
          if (data != null && data.Rows.Count > 0)
          {
            var num = 0;
            while (num < data.Rows.Count)
            {
              var artist = GetArtist(data.GetField(num, 0), Category.MusicFanartScraped);
              arrayList.Add(artist);
              checked { ++num; }
            }
          }
        }
        try
        {
          externalDatabaseManager1.Close();
        }
        catch { }
        return arrayList;
      }
      catch (Exception ex)
      {
        if (externalDatabaseManager1 != null)
          externalDatabaseManager1.Close();
        logger.Error("GetMusicVideoArtists: " + ex);
      }
      return null;
    }

    public static List<AlbumInfo> GetMusicVideoAlbums(string dbName)
    {
      var externalDatabaseManager1 = (ExternalDatabaseManager) null;
      var arrayList = new List<AlbumInfo>();
      try
      {
        externalDatabaseManager1 = new ExternalDatabaseManager();
        var str = string.Empty;
        if (externalDatabaseManager1.InitDB(dbName))
        {
          var data = externalDatabaseManager1.GetData(Category.MusicAlbumThumbScraped);
          if (data != null && data.Rows.Count > 0)
          {
            var num = 0;
            while (num < data.Rows.Count)
            {
              var album = new AlbumInfo();
              album.Artist      = GetArtist(data.GetField(num, 0), Category.MusicAlbumThumbScraped);
              album.AlbumArtist = album.Artist;
              album.Album       = GetAlbum(data.GetField(num, 1), Category.MusicAlbumThumbScraped);

              arrayList.Add(album);
              checked { ++num; }
            }
          }
        }
        try
        {
          externalDatabaseManager1.Close();
        }
        catch { }
        return arrayList;
      }
      catch (Exception ex)
      {
        if (externalDatabaseManager1 != null)
          externalDatabaseManager1.Close();
        logger.Error("GetMusicVideoAlbums: " + ex);
      }
      return null;
    }

    public static string GetArtistLeftOfMinusSign(string key, bool flag = false)
    {
      if (string.IsNullOrEmpty(key))
      {
        return string.Empty;
      }
      if ((flag) && (key.IndexOf(" - ", StringComparison.CurrentCulture) >= 0))
      {
        key = key.Substring(0, key.LastIndexOf(" - ", StringComparison.CurrentCulture));
      }
      else if (key.IndexOf("-", StringComparison.CurrentCulture) >= 0)
      {
        key = key.Substring(0, key.LastIndexOf("-", StringComparison.CurrentCulture));
      }
      return key.Trim();
    }

    public static string GetAlbumRightOfMinusSign(string key, bool flag = false)
    {
      if (string.IsNullOrEmpty(key))
      {
        return string.Empty;
      }
      if ((flag) && (key.IndexOf(" - ", StringComparison.CurrentCulture) >= 0))
      {
        key = key.Substring(checked (key.LastIndexOf(" - ", StringComparison.CurrentCulture) + 3));
      }
      else if (key.IndexOf("-", StringComparison.CurrentCulture) >= 0)
      {
        key = key.Substring(checked (key.LastIndexOf("-", StringComparison.CurrentCulture) + 1));
      }
      return key.Trim();
    }

    public static string GetFileName(string filename)
    {
      var result = string.Empty;
      try
      {
        if (!string.IsNullOrEmpty(filename))
        {
          result = Path.GetFileName(filename);
        }
      }
      catch
      {
        result = string.Empty;
      }
      return result;
    }

    public static string GetGetDirectoryName(string filename)
    {
      var result = string.Empty;
      try
      {
        if (!string.IsNullOrEmpty(filename))
        {
          result = Path.GetDirectoryName(filename);
        }
      }
      catch
      {
        result = string.Empty;
      }
      return result;
    }

    public static string RemoveExtension(string key)
    {
      if (string.IsNullOrEmpty(key))
      {
        return string.Empty;
      }
      key = Regex.Replace(key.Trim(), @"\.(jpe?g|png|bmp|tiff?|gif)$",string.Empty,RegexOptions.IgnoreCase);
      return key;
    }

    public static string RemoveDigits(string key)
    {
      if (string.IsNullOrEmpty(key))
        return string.Empty;
      else
        return Regex.Replace(key, @"\d", string.Empty);
    }

    public static string PatchSql(string s)
    {
      if (string.IsNullOrEmpty(s))
        return string.Empty;
      else
        return s.Replace("'", "''");
    }

    public static string RemoveResolutionFromFileName(string s, bool flag = false)
    {
      if (string.IsNullOrEmpty(s))
        return string.Empty;

      var old = string.Empty ;
      old = s.Trim() ;
      s = Regex.Replace(s.Trim(), @"(.*?\S\s)(\([^\s\d]+?\))(,|\s|$)","$1$3",RegexOptions.IgnoreCase);
      if (string.IsNullOrEmpty(s.Trim())) s = old ;

      old = s.Trim() ;
      s = Regex.Replace(s.Trim(), @"([^\S]|^)[\[\(]?loseless[\]\)]?([^\S]|$)","$1$2",RegexOptions.IgnoreCase);
      if (string.IsNullOrEmpty(s.Trim())) s = old ;

      old = s.Trim() ;
      s = Regex.Replace(s.Trim(), @"([^\S]|^)thumb(nail)?s?([^\S]|$)","$1$3",RegexOptions.IgnoreCase);
      if (string.IsNullOrEmpty(s.Trim())) s = old ;

      old = s.Trim() ;
      s = Regex.Replace(s.Trim(), @"\d{3,4}x\d{3,4}",string.Empty,RegexOptions.IgnoreCase);
      if (string.IsNullOrEmpty(s.Trim())) s = old ;

      old = s.Trim() ;
      s = Regex.Replace(s.Trim(), @"[-_]?[\[\(]?\d{3,4}(p|i)[\]\)]?",string.Empty,RegexOptions.IgnoreCase);
      if (string.IsNullOrEmpty(s.Trim())) s = old ;

      old = s.Trim() ;
      s = Regex.Replace(s.Trim(), @"([^\S]|^)([\-_]?[\[\(]?(720|1080|1280|1440|1714|1920|2160)[\]\)]?)","$1",RegexOptions.IgnoreCase);
      if (string.IsNullOrEmpty(s.Trim())) s = old ;

      old = s.Trim() ;
      s = Regex.Replace(s.Trim(), @"[\-_][\[\(]?(400|500|600|700|800|900|1000)[\]\)]?",string.Empty,RegexOptions.IgnoreCase);
      if (string.IsNullOrEmpty(s.Trim())) s = old ;

      old = s.Trim() ;
      s = Regex.Replace(s.Trim(), @"([^\S]|^)([\-_]?[\[\(]?(21|22|23|24|25|26|27|28|29)\d{2,}[\]\)]?)","$1",RegexOptions.IgnoreCase);
      if (string.IsNullOrEmpty(s.Trim())) s = old ;

      old = s.Trim() ;
      s = Regex.Replace(s.Trim(), @"([^\S]|^)([\-_]?[\[\(]?(3|4|5|6|7|8|9)\d{3,}[\]\)]?)","$1",RegexOptions.IgnoreCase);
      if (string.IsNullOrEmpty(s.Trim())) s = old ;
      if (flag)
      {
        old = s.Trim() ;
        s = Regex.Replace(s.Trim(), @"\s[\(\[_\.\-]?(?:cd|dvd|p(?:ar)?t|dis[ck])[ _\.\-]?[0-9]+[\)\]]?$",string.Empty,RegexOptions.IgnoreCase);
        if (string.IsNullOrEmpty(s.Trim())) s = old ;

        old = s.Trim() ;
        s = Regex.Replace(s.Trim(), @"([^\S]|^)(cd|mp3|ape|wre|flac|dvd)([^\S]|$)","$1$3",RegexOptions.IgnoreCase);
        if (string.IsNullOrEmpty(s.Trim())) s = old ;

        old = s.Trim() ;
        s = Regex.Replace(s.Trim(), @"([^\S]|^)(cd|mp3|ape|wre|flac|dvd)([^\S]|$)","$1$3",RegexOptions.IgnoreCase);
        if (string.IsNullOrEmpty(s.Trim())) s = old ;
      }
      s = Utils.TrimWhiteSpace(s.Trim());
      s = Utils.TrimWhiteSpace(s.Trim());
      return s;
    }

    public static string RemoveTrailingDigits(string s)
    {
      if (s == null)
        return string.Empty;
      if (IsInteger(s))
        return s;
      else
        return Regex.Replace(s, "[0-9]*$", string.Empty).Trim();
    }

    public static string MovePrefixToFront(this string self)
    {
      if (self == null)
        return string.Empty;
      else
        return new Regex(@"(.+?)(?: (the|a|an|ein|das|die|der|les|la|le|el|une|de|het))?\s*$", RegexOptions.IgnoreCase).Replace(self, "$2 $1").Trim();
    }

    public static string MovePrefixToBack(this string self)
    {
      if (self == null)
        return string.Empty;
      else
        return new Regex(@"^(the|a|an|ein|das|die|der|les|la|le|el|une|de|het)\s(.+)", RegexOptions.IgnoreCase).Replace(self, "$2, $1").Trim();
    }

    public static string GetAllVersionNumber()
    {
      return Assembly.GetExecutingAssembly().GetName().Version.ToString();
    }

    public static void Shuffle(ref Hashtable filenames)
    {
      if (filenames == null)
        return;

      try
      { 
        int n = filenames.Count;
        while (n > 1)
        {
          n--;
          int k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
          var value = filenames[k];
          filenames[k] = filenames[n];
          filenames[n] = value;
        }
      }
      catch  (Exception ex)
      {
        logger.Error("Shuffle: " + ex);
      }
    }

    public static bool IsIdle()
    {
      try
      {
        if ((DateTime.Now - GUIGraphicsContext.LastActivity).TotalMilliseconds >= IdleTimeInMillis)
          return true;
      }
      catch (Exception ex)
      {
        logger.Error("IsIdle: " + ex);
      }
      return false;
    }

    public static bool ShouldRefreshRecording()
    {
      try
      {
        if ((DateTime.Now - LastRefreshRecording).TotalMilliseconds >= 600000.0)
          return true;
      }
      catch (Exception ex)
      {
        logger.Error("ShouldRefreshRecording: " + ex);
      }
      return false;
    }

    public static void AddPictureToCache(string property, string value, ref ArrayList al)
    {
      if (string.IsNullOrEmpty(value))
        return;

      if (al == null)
        return;

      if (al.Contains(value))
        return;

      try
      {
        al.Add(value);
      }
      catch (Exception ex)
      {
        logger.Error("AddPictureToCache: " + ex);
      }
      LoadImage(value);
    }

    public static void LoadImage(string filename)
    {
      if (isStopping)
        return;

      if (string.IsNullOrEmpty(filename))
        return;

      try
      {
        GUITextureManager.Load(filename, 0L, 0, 0, true);
      }
      catch (Exception ex)
      {
        logger.Error("LoadImage (" + filename + "): " + ex);
      }
    }

    public static void EmptyAllImages(ref ArrayList al)
    {
      try
      {
        if (al == null || al.Count <= 0)
          return;

        foreach (var obj in al)
        {
          if (obj != null)
            UNLoadImage(obj.ToString());
        }
        al.Clear();
      }
      catch (Exception ex)
      {
        logger.Error("EmptyAllImages: " + ex);
      }
    }

    private static void UNLoadImage(string filename)
    {
      try
      {
        GUITextureManager.ReleaseTexture(filename);
      }
      catch (Exception ex)
      {
        logger.Error("UnLoadImage (" + filename + "): " + ex);
      }
    }

    [DllImport("gdiplus.dll", CharSet = CharSet.Unicode)]
    private static extern int GdipLoadImageFromFile(string filename, out IntPtr image);

    public static Image LoadImageFastFromMemory (string filename) 
    { 
      using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(filename)))
      {
        try
        {
          return Image.FromStream(ms, false, false); 
        }
        catch
        {
          return null;
        }
      }
    }

    public static Image LoadImageFastFromFile(string filename)
    {
      var image1 = IntPtr.Zero;
      Image image2;
      try
      {
        if (GdipLoadImageFromFile(filename, out image1) != 0)
        {
          logger.Warn("GdipLoadImageFromFile: gdiplus.dll method failed. Will degrade performance.");
          image2 = Image.FromFile(filename);
        }
        else
          image2 = (Image) typeof (Bitmap).InvokeMember("FromGDIplus", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.InvokeMethod, null, null, new object[1]
          {
            image1
          });
      }
      catch (Exception ex)
      {
        logger.Error("GdipLoadImageFromFile: Failed to load image from " + filename+ " - " + ex);
        image2 = null;
      }
      return image2;
    }

    public static Image ApplyInvert(Image bmpImage)
    {  
      byte A, R, G, B;  
      Color pixelColor;  
      Bitmap bitmapImage = (Bitmap)bmpImage.Clone();

      for (int y = 0; y < bitmapImage.Height; y++)  
      {  
        for (int x = 0; x < bitmapImage.Width; x++)  
        {  
          pixelColor = bitmapImage.GetPixel(x, y);  
          A = (byte)(255 - pixelColor.A); 
          R = pixelColor.R;  
          G = pixelColor.G;  
          B = pixelColor.B;  
          bitmapImage.SetPixel(x, y, Color.FromArgb((int)A, (int)R, (int)G, (int)B));  
        }  
      }
      return bitmapImage;  
    }

    #region Selected Item
    public static string GetSelectedMyVideoTitle()
    {
      string result = string.Empty;

      if (iActiveWindow == (int)GUIWindow.Window.WINDOW_INVALID)
        return result;

      try
      {
        if (iActiveWindow == 2003 ||  // Dialog Video Info
            iActiveWindow == 6 ||     // My Video
            iActiveWindow == 25 ||    // My Video Title
            iActiveWindow == 614 ||   // Dialog Video Artist Info
            iActiveWindow == 28       // My Video Play List
           )
        {
          result = (iActiveWindow != 2003 ? Utils.GetProperty("#selecteditem") : Utils.GetProperty("#title"));
        }
      }
      catch (Exception ex)
      {
        logger.Error("GetSelectedTitle: " + ex);
      }
      return result;
    }

    public static void GetSelectedItem(ref string SelectedItem, ref string SelectedAlbum, ref string SelectedGenre, ref string SelectedStudios, ref bool isMusicVideo)
    {
      try
      {
        if (iActiveWindow == (int)GUIWindow.Window.WINDOW_INVALID)
          return;

        #region SelectedItem
        if (iActiveWindow == 6623)       // mVids plugin - Outdated.
        {
          SelectedItem = Utils.GetProperty("#mvids.artist");
          SelectedItem = Utils.GetArtistLeftOfMinusSign(SelectedItem);
        }
        else if (iActiveWindow == 47286) // Rockstar plugin
        {
          SelectedItem = Utils.GetProperty("#Rockstar.SelectedTrack.ArtistName");
          SelectedAlbum = Utils.GetProperty("#Rockstar.SelectedTrack.AlbumName") ;
        }
        else if (iActiveWindow == 759)     // My TV Recorder
          SelectedItem = Utils.GetProperty("#TV.RecordedTV.Title");
        else if (iActiveWindow == 1)       // My TV View
          SelectedItem = Utils.GetProperty("#TV.View.title");
        else if (iActiveWindow == 600)     // My TV Guide
          SelectedItem = Utils.GetProperty("#TV.Guide.Title");
        else if (iActiveWindow == 880)     // MusicVids plugin
          SelectedItem = Utils.GetProperty("#MusicVids.ArtistName");
        else if (iActiveWindow == 510 ||   // My Music Plaing Now - Why is it here? 
                 iActiveWindow == 90478 || // My Lyrics - Why is it here? 
                 iActiveWindow == 25652 || // Radio Time - Why is it here? 
                 iActiveWindow == 35)      // Basic Home - Why is it here? And where there may appear tag: #Play.Current.Title
        {
          SelectedItem = string.Empty;

          // mvCentral
          var mvcArtist = Utils.GetProperty("#Play.Current.mvArtist");
          var mvcAlbum = Utils.GetProperty("#Play.Current.mvAlbum");
          var mvcPlay = Utils.GetProperty("#mvCentral.isPlaying");

          var selAlbumArtist = Utils.GetProperty("#Play.Current.AlbumArtist");
          var selArtist = Utils.GetProperty("#Play.Current.Artist");
          var selTitle = Utils.GetProperty("#Play.Current.Title");

          if (!string.IsNullOrEmpty(selArtist))
            if (!string.IsNullOrEmpty(selAlbumArtist))
              if (selArtist.Equals(selAlbumArtist, StringComparison.InvariantCultureIgnoreCase))
                SelectedItem = selArtist;
              else
                SelectedItem = selArtist + '|' + selAlbumArtist;
            else
              SelectedItem = selArtist;
          /*
          if (!string.IsNullOrEmpty(tuneArtist))
            SelectedItem = SelectedItem + (string.IsNullOrEmpty(SelectedItem) ? "" : "|") + tuneArtist; 
          */
          SelectedAlbum = Utils.GetProperty("#Play.Current.Album");
          SelectedGenre = Utils.GetProperty("#Play.Current.Genre");

          if (!string.IsNullOrEmpty(selArtist) && !string.IsNullOrEmpty(selTitle) && string.IsNullOrEmpty(SelectedAlbum))
          {
            Scraper scraper = new Scraper();
            SelectedAlbum = scraper.LastFMGetAlbum (selArtist, selTitle);
            scraper = null;
          }
          if (!string.IsNullOrEmpty(selAlbumArtist) && !string.IsNullOrEmpty(selTitle) && string.IsNullOrEmpty(SelectedAlbum))
          {
            Scraper scraper = new Scraper();
            SelectedAlbum = scraper.LastFMGetAlbum (selAlbumArtist, selTitle);
            scraper = null;
          }
          /*
          if (!string.IsNullOrEmpty(tuneArtist) && !string.IsNullOrEmpty(tuneTrack) && string.IsNullOrEmpty(tuneAlbum) && string.IsNullOrEmpty(SelectedAlbum))
          {
            Scraper scraper = new Scraper();
            SelectedAlbum = scraper.LastFMGetAlbum (tuneArtist, tuneTrack);
            scraper = null;
          }
          */
          if (!string.IsNullOrEmpty(mvcPlay) && mvcPlay.Equals("true",StringComparison.CurrentCulture))
          {
            isMusicVideo = true;
            if (!string.IsNullOrEmpty(mvcArtist))
              SelectedItem = SelectedItem + (string.IsNullOrEmpty(SelectedItem) ? "" : "|") + mvcArtist; 
            if (string.IsNullOrEmpty(SelectedAlbum))
              SelectedAlbum = string.Empty + mvcAlbum;
          }

          if (string.IsNullOrEmpty(SelectedItem) && string.IsNullOrEmpty(selArtist) && string.IsNullOrEmpty(selAlbumArtist))
            SelectedItem = selTitle;
        }
        else if (iActiveWindow == 6622)    // Music Trivia 
        {
          SelectedItem = Utils.GetProperty("#selecteditem2");
          SelectedItem = Utils.GetArtistLeftOfMinusSign(SelectedItem);
        }
        else if (iActiveWindow == 2003 ||  // Dialog Video Info
                 iActiveWindow == 6 ||     // My Video
                 iActiveWindow == 25 ||    // My Video Title
                 iActiveWindow == 614 ||   // Dialog Video Artist Info
                 iActiveWindow == 28       // My Video Play List
                )
        {
          var movieID = Utils.GetProperty("#movieid");
          var selectedTitle = (iActiveWindow != 2003 ? Utils.GetProperty("#selecteditem") : Utils.GetProperty("#title"));
          SelectedItem = (movieID == null || movieID == string.Empty || movieID == "-1" || movieID == "0") ? selectedTitle : movieID;
          SelectedGenre = Utils.GetProperty("#genre");
          SelectedStudios = Utils.GetProperty("#studios");
          // logger.Debug("*** "+movieID+" - "+Utils.GetProperty("#selecteditem")+" - "+Utils.GetProperty("#title")+" - "+Utils.GetProperty("#myvideosuserfanart")+" -> "+SelectedItem+" - "+SelectedGenre);
        }
        else if (iActiveWindow == 96742)     // Moving Pictures
        {
          SelectedItem = Utils.GetProperty("#selecteditem");
          SelectedStudios = Utils.GetProperty("#MovingPictures.SelectedMovie.studios");
          SelectedGenre = Utils.GetProperty("#MovingPictures.SelectedMovie.genres");
          // logger.Debug("*** "+SelectedItem+" - "+SelectedStudios+" - "+SelectedGenre);
        }
        else if (iActiveWindow == 9811 ||    // TVSeries
                 iActiveWindow == 9813)      // TVSeries Playlist
        {
          SelectedItem = UtilsTVSeries.GetTVSeriesAttributes(ref SelectedGenre, ref SelectedStudios);
          if (string.IsNullOrEmpty(SelectedItem))
          {
            SelectedItem = Utils.GetProperty("#TVSeries.Title"); 
          }
          if (string.IsNullOrEmpty(SelectedStudios))
          {
            SelectedStudios = Utils.GetProperty("#TVSeries.Series.Network");
          }
          if (string.IsNullOrEmpty(SelectedGenre))
          {
            SelectedGenre = Utils.GetProperty("#TVSeries.Series.Genre");
          }
          // logger.Debug("*** TVSeries: " + SelectedItem + " - " + SelectedStudios + " - " + SelectedGenre);
        }
        else if (iActiveWindow == 112011 ||  // mvCentral
                 iActiveWindow == 112012 ||  // mvCentral Playlist
                 iActiveWindow == 112013 ||  // mvCentral StatsAndInfo
                 iActiveWindow == 112015)    // mvCentral SmartDJ
        {
          SelectedItem = Utils.GetProperty("#mvCentral.ArtistName");

          SelectedAlbum = Utils.GetProperty("#mvCentral.Album");
          SelectedGenre = Utils.GetProperty("#mvCentral.Genre");

          var mvcIsPlaying = Utils.GetProperty("#mvCentral.isPlaying");
          if (!string.IsNullOrEmpty(mvcIsPlaying) && mvcIsPlaying.Equals("true",StringComparison.CurrentCulture))
          {
            isMusicVideo = true;
          }
        }
        else if (iActiveWindow == 25650)     // Radio Time
        {
          SelectedItem = Utils.GetProperty("#RadioTime.Selected.Subtext"); // Artist - Track || TODO for: Artist - Album - Track
          SelectedItem = Utils.GetArtistLeftOfMinusSign(SelectedItem, true);
        }
        else if (iActiveWindow == 29050 || // youtube.fm videosbase
                 iActiveWindow == 29051 || // youtube.fm playlist
                 iActiveWindow == 29052    // youtube.fm info
                )
        {
          SelectedItem = Utils.GetProperty("#selecteditem");
          SelectedItem = Utils.GetArtistLeftOfMinusSign(SelectedItem);
        }
        else if (iActiveWindow == 30885)   // GlobalSearch Music
        {
          SelectedItem = Utils.GetProperty("#selecteditem");
          SelectedItem = Utils.GetArtistLeftOfMinusSign(SelectedItem);
        }
        else if (iActiveWindow == 30886)   // GlobalSearch Music Details
        {
          try
          {
            if (GUIWindowManager.GetWindow(iActiveWindow).GetControl(1) != null)
              SelectedItem = ((GUIFadeLabel) GUIWindowManager.GetWindow(iActiveWindow).GetControl(1)).Label;
          }
          catch { }
        }
        else
          SelectedItem = Utils.GetProperty("#selecteditem");

        SelectedAlbum   = (string.IsNullOrEmpty(SelectedAlbum) ? null : SelectedAlbum); 
        SelectedGenre   = (string.IsNullOrEmpty(SelectedGenre) ? null : SelectedGenre.Replace(" / ", "|").Replace(", ", "|")); 
        SelectedStudios = (string.IsNullOrEmpty(SelectedStudios) ? null : SelectedStudios.Replace(" / ", "|").Replace(", ", "|")); 
        #endregion
      }
      catch (Exception ex)
      {
        logger.Error("GetSelectedItem: " + ex);
      }
    }
    #endregion

    #region Music Items                                                                                                         
    public static void GetCurrMusicPlayItem(ref string CurrentTrackTag, ref string CurrentAlbumTag, ref string CurrentGenreTag, ref string LastArtistTrack, ref string LastAlbumArtistTrack)
    {
      try
      {
        #region Fill current tags
        if (Utils.iActiveWindow == 730718) // MP Grooveshark
        {
          CurrentTrackTag = Utils.GetProperty("#mpgrooveshark.current.artist");
          CurrentAlbumTag = Utils.GetProperty("#mpgrooveshark.current.album");
          CurrentGenreTag = null;
        }
        else
        {
          CurrentTrackTag = string.Empty;

          // Common play
          var selAlbumArtist = Utils.GetProperty("#Play.Current.AlbumArtist").Trim();
          var selArtist = Utils.GetProperty("#Play.Current.Artist").Trim();
          var selTitle = Utils.GetProperty("#Play.Current.Title").Trim();
          // Radio Time
          /*
          var tuneArtist = Utils.GetProperty("#RadioTime.Play.Artist");
          var tuneAlbum = Utils.GetProperty("#RadioTime.Play.Album");
          var tuneTrack = Utils.GetProperty("#RadioTime.Play.Song");
          */
          // mvCentral
          var mvcArtist = Utils.GetProperty("#Play.Current.mvArtist");
          var mvcAlbum = Utils.GetProperty("#Play.Current.mvAlbum");
          var mvcPlay = Utils.GetProperty("#mvCentral.isPlaying");

          if (!string.IsNullOrEmpty(selArtist))
            if (!string.IsNullOrEmpty(selAlbumArtist))
              if (selArtist.Equals(selAlbumArtist, StringComparison.InvariantCultureIgnoreCase))
                CurrentTrackTag = selArtist;
              else
                CurrentTrackTag = selArtist + '|' + selAlbumArtist;
            else
              CurrentTrackTag = selArtist;
          /*
          if (!string.IsNullOrEmpty(tuneArtist))
            CurrentTrackTag = CurrentTrackTag + (string.IsNullOrEmpty(CurrentTrackTag) ? "" : "|") + tuneArtist; 
          */
          CurrentAlbumTag = Utils.GetProperty("#Play.Current.Album");
          CurrentGenreTag = Utils.GetProperty("#Play.Current.Genre");

          if (!string.IsNullOrEmpty(selArtist) && !string.IsNullOrEmpty(selTitle) && string.IsNullOrEmpty(CurrentAlbumTag))
          {
            if (!LastArtistTrack.Equals(selArtist+"#"+selTitle, StringComparison.CurrentCulture))
            {
              Scraper scraper = new Scraper();
              CurrentAlbumTag = scraper.LastFMGetAlbum(selArtist, selTitle);
              scraper = null;
              LastArtistTrack = selArtist+"#"+selTitle;
            }
          }
          if (!string.IsNullOrEmpty(selAlbumArtist) && !string.IsNullOrEmpty(selTitle) && string.IsNullOrEmpty(CurrentAlbumTag))
          {
            if (!LastAlbumArtistTrack.Equals(selAlbumArtist+"#"+selTitle, StringComparison.CurrentCulture))
            {
              Scraper scraper = new Scraper();
              CurrentAlbumTag = scraper.LastFMGetAlbum(selAlbumArtist, selTitle);
              scraper = null;
              LastAlbumArtistTrack = selAlbumArtist+"#"+selTitle;
            }
          }
          /*
          if (!string.IsNullOrEmpty(tuneArtist) && !string.IsNullOrEmpty(tuneTrack) && string.IsNullOrEmpty(tuneAlbum) && string.IsNullOrEmpty(CurrentAlbumTag))
          {
            Scraper scraper = new Scraper();
            CurrentAlbumTag = scraper.LastFMGetAlbum (tuneArtist, tuneTrack);
            scraper = null;
          }
          */
          if (!string.IsNullOrEmpty(mvcPlay) && mvcPlay.Equals("true",StringComparison.CurrentCulture))
          {
            if (!string.IsNullOrEmpty(mvcArtist))
              CurrentTrackTag = CurrentTrackTag + (string.IsNullOrEmpty(CurrentTrackTag) ? "" : "|") + mvcArtist; 
            if (string.IsNullOrEmpty(CurrentAlbumTag))
              CurrentAlbumTag = string.Empty + mvcAlbum;
          }
        }
        #endregion
      }
      catch (Exception ex)
      {
        logger.Error("GetCurrMusicPlayItem: " + ex);
      }
    }

    public static string GetMusicArtistFromListControl(ref string currSelectedMusicAlbum)
    {
      try
      {
        if (iActiveWindow == (int)GUIWindow.Window.WINDOW_INVALID)
          return null;

        var selectedListItem = GUIControl.GetSelectedListItem(iActiveWindow, 50);
        if (selectedListItem == null)
          return null;

        // if (selectedListItem.MusicTag == null && selectedListItem.Label.Equals("..", StringComparison.CurrentCulture))
        //   return "..";

        var selAlbumArtist = Utils.GetProperty("#music.albumArtist");
        var selArtist = Utils.GetProperty("#music.artist");
        var selAlbum = Utils.GetProperty("#music.album");
        var selItem = Utils.GetProperty("#selecteditem");

        if (!string.IsNullOrEmpty(selAlbum))
          currSelectedMusicAlbum = selAlbum ;

        // logger.Debug("*** GMAFLC: 1 - ["+selArtist+"] ["+selAlbumArtist+"] ["+selAlbum+"] ["+selItem+"]");
        if (!string.IsNullOrEmpty(selArtist))
          if (!string.IsNullOrEmpty(selAlbumArtist))
            if (selArtist.Equals(selAlbumArtist, StringComparison.InvariantCultureIgnoreCase))
              return selArtist;
            else
              return selArtist + '|' + selAlbumArtist;
          else
            return selArtist;
        else
          if (!string.IsNullOrEmpty(selAlbumArtist))
            return selAlbumArtist;

        if (selectedListItem.MusicTag == null)
        {
          var musicDB = MusicDatabase.Instance;
          var list = new List<SongMap>();
          musicDB.GetSongsByPath(selectedListItem.Path, ref list);
          if (list != null)
          {
            using (var enumerator = list.GetEnumerator())
            {
              if (enumerator.MoveNext())
              {
                currSelectedMusicAlbum = enumerator.Current.m_song.Album.Trim() ;
                // return Utils.MovePrefixToBack(Utils.RemoveMPArtistPipes(enumerator.Current.m_song.Artist))+"|"+enumerator.Current.m_song.Artist+"|"+enumerator.Current.m_song.AlbumArtist;
                // logger.Debug("*** GMAFLC: 2 - ["+enumerator.Current.m_song.Artist+"] ["+enumerator.Current.m_song.AlbumArtist+"]");
                return Utils.RemoveMPArtistPipes(enumerator.Current.m_song.Artist)+"|"+enumerator.Current.m_song.Artist+"|"+enumerator.Current.m_song.AlbumArtist;
              }
            }
          }

          if (selItem.Equals("..", StringComparison.CurrentCulture))
            return selItem;

          var FoundArtist = (string) null;
          //
          var SelArtist = Utils.MovePrefixToBack(Utils.RemoveMPArtistPipes(Utils.GetArtistLeftOfMinusSign(selItem)));
          var arrayList = new ArrayList();
          musicDB.GetAllArtists(ref arrayList);
          var index = 0;
          while (index < arrayList.Count)
          {
            var MPArtist = Utils.MovePrefixToBack(Utils.RemoveMPArtistPipes(arrayList[index].ToString()));
            if (SelArtist.IndexOf(MPArtist, StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
              FoundArtist = MPArtist;
              break;
            }
            checked { ++index; }
          }
          if (arrayList != null)
            arrayList.Clear();
          arrayList = null;
          // logger.Debug("*** GMAFLC: 3 - ["+FoundArtist+"]");
          if (!string.IsNullOrEmpty(FoundArtist))
            return FoundArtist;
          //
          SelArtist = Utils.GetArtistLeftOfMinusSign(selItem);
          arrayList = new ArrayList();
          if (musicDB.GetAlbums(3, SelArtist, ref arrayList))
          {
            var albumInfo = (AlbumInfo) arrayList[0];
            if (albumInfo != null)
            {
              FoundArtist = (albumInfo.Artist == null || albumInfo.Artist.Length <= 0 ? albumInfo.AlbumArtist : albumInfo.Artist + 
                            (albumInfo.AlbumArtist == null || albumInfo.AlbumArtist.Length <= 0 ? string.Empty : "|" + albumInfo.AlbumArtist));
              currSelectedMusicAlbum = albumInfo.Album.Trim() ;
            }
          }
          if (arrayList != null)
            arrayList.Clear();
          arrayList = null;
          // logger.Debug("*** GMAFLC: 4 - ["+FoundArtist+"]");
          if (!string.IsNullOrEmpty(FoundArtist))
            return FoundArtist;
          //
          // var str3 = Utils.MovePrefixToBack(Utils.RemoveMPArtistPipes(Utils.GetArtistLeftOfMinusSign(artistLeftOfMinusSign)));
          // var SelArtistWithoutPipes = Utils.RemoveMPArtistPipes(Utils.GetArtistLeftOfMinusSign(SelArtist));
          var SelArtistWithoutPipes = Utils.RemoveMPArtistPipes(SelArtist);
          arrayList = new ArrayList();
          if (musicDB.GetAlbums(3, SelArtistWithoutPipes, ref arrayList))
          {
            var albumInfo = (AlbumInfo) arrayList[0];
            if (albumInfo != null)
            {
              FoundArtist = (albumInfo.Artist == null || albumInfo.Artist.Length <= 0 ? albumInfo.AlbumArtist : albumInfo.Artist + 
                            (albumInfo.AlbumArtist == null || albumInfo.AlbumArtist.Length <= 0 ? string.Empty : "|" + albumInfo.AlbumArtist));
              currSelectedMusicAlbum = albumInfo.Album.Trim() ;
            }
          }
          if (arrayList != null)
            arrayList.Clear();
          arrayList = null;
          // return Utils.MovePrefixToBack(Utils.RemoveMPArtistPipes(s));
          // logger.Debug("*** GMAFLC: 5 - ["+FoundArtist+"]");
          if (!string.IsNullOrEmpty(FoundArtist))
            return FoundArtist;
        }
        else
        {
          var musicTag = (MusicTag) selectedListItem.MusicTag;
          if (musicTag == null)
            return null;

          selArtist = string.Empty ;
          selAlbumArtist = string.Empty ;

          if (!string.IsNullOrEmpty(musicTag.Album))
            currSelectedMusicAlbum = musicTag.Album.Trim();

          if (!string.IsNullOrEmpty(musicTag.Artist))
            // selArtist = Utils.MovePrefixToBack(Utils.RemoveMPArtistPipes(musicTag.Artist)).Trim();
            selArtist = Utils.RemoveMPArtistPipes(musicTag.Artist).Trim()+"|"+musicTag.Artist.Trim();
          if (!string.IsNullOrEmpty(musicTag.AlbumArtist))
            // selAlbumArtist = Utils.MovePrefixToBack(Utils.RemoveMPArtistPipes(musicTag.AlbumArtist)).Trim();
            selAlbumArtist = Utils.RemoveMPArtistPipes(musicTag.AlbumArtist).Trim()+"|"+musicTag.AlbumArtist.Trim();

          // logger.Debug("*** GMAFLC: 6 - ["+selArtist+"] ["+selAlbumArtist+"]");
          if (!string.IsNullOrEmpty(selArtist))
            if (!string.IsNullOrEmpty(selAlbumArtist))
              if (selArtist.Equals(selAlbumArtist, StringComparison.InvariantCultureIgnoreCase))
                return selArtist;
              else
                return selArtist + '|' + selAlbumArtist;
            else
              return selArtist;
          else
            if (!string.IsNullOrEmpty(selAlbumArtist))
              return selAlbumArtist;
        }
        // logger.Debug("*** GMAFLC: 7 - ["+selItem+"]");
        return selItem;
      }
      catch (Exception ex)
      {
        logger.Error("getMusicArtistFromListControl: " + ex);
      }
      return null;
    }
    #endregion

    public static string GetRandomDefaultBackdrop(ref string currFile, ref int iFilePrev)
    {
      var result = string.Empty;
      try
      {
        if (!GetIsStopping())
        {
          if (UseDefaultBackdrop)
          {
            if (DefaultBackdropImages != null)
            {
              if (DefaultBackdropImages.Count > 0)
              {
                if (iFilePrev == -1)
                  Shuffle(ref defaultBackdropImages);

                var htValues = DefaultBackdropImages.Values;
                result = GetFanartFilename(ref iFilePrev, ref currFile, ref htValues, Category.MusicFanartScraped);
              }
            }
          }
        }
      }
      catch (Exception ex)
      {
        logger.Error("GetRandomDefaultBackdrop: " + ex);
      }
      return result;
    }

    public static string GetRandomSlideShowImages(ref string currFile, ref int iFilePrev)
    {
      var result = string.Empty;
      try
      {
        if (!GetIsStopping())
        {
          if (UseMyPicturesSlideShow)
          {
            if (SlideShowImages != null)
            {
              if (SlideShowImages.Count > 0)
              {
                if (iFilePrev == -1)
                  Shuffle(ref slideshowImages);

                var htValues = SlideShowImages.Values;
                result = GetFanartFilename(ref iFilePrev, ref currFile, ref htValues, Category.MusicFanartScraped);
              }
            }
          }
        }
      }
      catch (Exception ex)
      {
        logger.Error("GetRandomSlideShowImages: " + ex);
      }
      return result;
    }

    public static string GetFanartFilename(ref int iFilePrev, ref string sFileNamePrev, ref ICollection htValues, Category category, bool recursion = false)
    {
      var result = string.Empty;
      // result = sFileNamePrev;
      try
      {
        if (!GetIsStopping())
        {
          if (htValues != null)
          {
            if (htValues.Count > 0)
            {
              var i = 0;
              var found = false;
              foreach (FanartImage fanartImage in htValues)
              {
                if (i <= iFilePrev)
                {
                  checked { ++i; }
                  continue;
                }

                if (CheckImageResolution(fanartImage.DiskImage, category, UseAspectRatio))
                {
                  result = fanartImage.DiskImage;
                  iFilePrev = i;
                  sFileNamePrev = result;
                  found = true;
                  break;
                }
                checked { ++i; }
              }

              if (!recursion && !found)
              {
                iFilePrev = -1;
                result = GetFanartFilename(ref iFilePrev, ref sFileNamePrev, ref htValues, category, true);
              }
            }
          }
        }
      }
      catch (Exception ex)
      {
        logger.Error("GetFanartFilename: " + ex);
      }
      return result;
    }

    /// <summary>
    /// Scan Folder for files by Mask and Import it to Database
    /// </summary>
    /// <param name="s">Folder</param>
    /// <param name="filter">Mask</param>
    /// <param name="category">Picture Category</param>
    /// <param name="ht"></param>
    /// <param name="provider">Picture Provider</param>
    /// <returns></returns>
    public static void SetupFilenames(string s, string filter, Category category, Hashtable ht, Provider provider, bool SubFolders = false)
    {
      if (provider == Provider.MusicFolder)
      {
        if (string.IsNullOrEmpty(MusicFoldersArtistAlbumRegex))
          return;
      }

      try
      {
        // logger.Debug("*** SetupFilenames: "+category.ToString()+" "+provider.ToString()+" folder: "+s+ " mask: "+filter);
        if (Directory.Exists(s))
        {
          var allFilenames = dbm.GetAllFilenames((category == Category.MusicFanartAlbum ? Category.MusicFanartManual : category));
          var localfilter = (provider != Provider.MusicFolder)
                               ? string.Format("^{0}$", filter.Replace(".", @"\.").Replace("*", ".*").Replace("?", ".").Replace("jpg", "(j|J)(p|P)(e|E)?(g|G)").Trim())
                               : string.Format(@"\\{0}$", filter.Replace(".", @"\.").Replace("*", ".*").Replace("?", ".").Trim()) ;
          // logger.Debug("*** SetupFilenames: "+category.ToString()+" "+provider.ToString()+" filter: " + localfilter);
          foreach (var FileName in Enumerable.Select<FileInfo, string>(Enumerable.Where<FileInfo>(new DirectoryInfo(s).GetFiles("*.*", SearchOption.AllDirectories), fi =>
          {
            return Regex.IsMatch(fi.FullName, localfilter, ((provider != Provider.MusicFolder) ? RegexOptions.CultureInvariant : RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) ;
          }), fi => fi.FullName))
          {
            if (allFilenames == null || !allFilenames.Contains(FileName))
            {
              if (!GetIsStopping())
              {
                var artist = string.Empty;
                var album = string.Empty;

                if (provider != Provider.MusicFolder)
                {
                  artist = GetArtist(FileName, category).Trim();
                  album = GetAlbum(FileName, category).Trim();
                }
                else // Fanart from Music folders 
                {
                  var fnWithoutFolder = string.Empty;
                  try
                  {
                    fnWithoutFolder = FileName.Substring(checked (s.Length));
                  }
                  catch
                  { 
                    fnWithoutFolder = FileName; 
                  }
                  artist = RemoveResolutionFromFileName(GetArtist(GetArtistFromFolder(fnWithoutFolder, MusicFoldersArtistAlbumRegex), category), true).Trim();
                  album = RemoveResolutionFromFileName(GetAlbum(GetAlbumFromFolder(fnWithoutFolder, MusicFoldersArtistAlbumRegex), category), true).Trim();
                  if (!string.IsNullOrEmpty(artist))
                    logger.Debug("For Artist: [" + artist + "] Album: ["+album+"] fanart found: "+FileName);
                }
                // logger.Debug("*** SetupFilenames: "+category.ToString()+" "+provider.ToString()+" artist: " + artist + " album: "+album+" - "+FileName);
                if (!string.IsNullOrEmpty(artist))
                {
                  if (ht != null && ht.Contains(artist))
                  {
                    dbm.LoadFanart(ht[artist].ToString(), FileName, FileName, category, album, provider, null, null);
                    // if (category == Category.TvSeriesScraped)
                    //   dbm.LoadFanart(artist, FileName, FileName, category, album, provider, null, null);
                  }
                  else
                    dbm.LoadFanart(artist, FileName, FileName, category, album, provider, null, null);
                }
              }
              else
                break;
            }
          }

          if ((ht == null) && (SubFolders))
            // Include Subfolders
            foreach (var SubFolder in Directory.GetDirectories(s))
              SetupFilenames(SubFolder, filter, category, ht, provider, SubFolders);
        }

        if (ht != null)
          ht.Clear();
        ht = null;
      }
      catch (Exception ex)
      {
        logger.Error("SetupFilenames: " + ex);
      }
    }

    public static List<string> LoadPathToAllFiles(string pathToFolder, string fileMask, int numberOfFilesToReturn, bool allDir)
    {
      var DirInfo = new DirectoryInfo(pathToFolder);
      var firstFiles = DirInfo.EnumerateFiles(fileMask, (allDir ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)).Take(numberOfFilesToReturn).ToList();
      return firstFiles.Select(l => l.FullName).ToList();
    }

    public static bool IsFileValid(string filename)
    {
      var flag = false;

      if (string.IsNullOrEmpty(filename))
        return flag;
      
      if (!File.Exists(filename))
        return flag;

      var TestImage = (Image) null;
      try
      {
        TestImage = LoadImageFastFromFile(filename);
        flag = (TestImage != null && TestImage.Width > 0 && TestImage.Height > 0);
      }
      catch 
      { 
        flag = false;
      }

      if (TestImage != null)
        TestImage.Dispose();

      return flag;
    }

    public static bool CheckImageResolution(string filename, Category category, bool UseAspectRatio)
    {
      if (string.IsNullOrEmpty(filename))
        return false;

      try
      {
        if (!File.Exists(filename))
        {
          dbm.DeleteImage(filename);
          return false;
        }
        else
        {
          var image = LoadImageFastFromFile(filename); // Image.FromFile(filename);
          if (image != null)
          {
            var imgWidth = (double) image.Width;
            var imgHeight = (double) image.Height;
            image.Dispose();
            // 3.5 SetImageRatio(filename, 0.0, imgWidth, imgHeight)
            if (imgWidth > 0.0 && imgHeight > 0.0) 
            {
              return imgWidth >= MinWResolution && imgHeight >= MinHResolution && (!UseAspectRatio || (imgHeight > 0.0 && imgWidth / imgHeight >= 1.3));
            }
          }
        }
      }
      catch (Exception ex)
      {
        logger.Error("CheckImageResolution: " + ex);
      }
      return false;
    }

    public static bool AllowFanartInActiveWindow()
    {                              
      return (iActiveWindow != 511 &&    // Music Full Screen
              iActiveWindow != 2005 &&   // Video Full Screen
              iActiveWindow != 602);     // My TV Full Screen
    }

    public static bool IsDirectoryEmpty (string path) 
    { 
      // string[] dirs = System.IO.Directory.GetDirectories( path ); 
      string[] files = System.IO.Directory.GetFiles( path ); 
      return /*dirs.Length == 0 &&*/ files.Length == 0;
    }

    public static int GetFilesCountByMask (string path, string mask) 
    { 
      string[] files = System.IO.Directory.GetFiles( path, mask, SearchOption.TopDirectoryOnly ); 
      return files.Length;
    }

    /* .Net 4.0
    public static bool IsDirectoryEmpty(string path)
    {
        return !Directory.EnumerateFileSystemEntries(path).Any();
    }
    */

    /// <summary>
    /// Return a themed version of the requested skin filename, or default skin filename, otherwise return the default fanart filename.  Use a path to media to get images.
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    public static string GetThemedSkinFile(string filename)
    {
      if (File.Exists(filename)) // sometimes filename is full path, don't know why
      {
        return filename;
      }
      else
      {
        return File.Exists(GUIGraphicsContext.Theme + filename) ? 
                 GUIGraphicsContext.Theme + filename : 
                 File.Exists(GUIGraphicsContext.Skin + filename) ? 
                   GUIGraphicsContext.Skin + filename : 
                   FAHFolder + filename;
      }
    }

    /// <summary>
    /// Return a themed version of the requested directory, or default skin directory, otherwise return the default fanart directory.
    /// </summary>
    /// <param name="dir"></param>
    /// <returns></returns>
    public static string GetThemedSkinDirectory(string dir)
    {
      return Directory.Exists(GUIGraphicsContext.Theme + dir) ? 
               GUIGraphicsContext.Theme + dir : 
               Directory.Exists(GUIGraphicsContext.Skin + dir) ? 
                 GUIGraphicsContext.Skin + dir : 
                 FAHFolder + dir;
    }

    public static string GetThemeFolder(string path)
    {
      if (string.IsNullOrEmpty(GUIGraphicsContext.ThemeName))
      {
        return string.Empty;
      }

      var tThemeDir = path+@"Themes\"+GUIGraphicsContext.ThemeName.Trim()+@"\";
      if (Directory.Exists(tThemeDir))
      {
        return tThemeDir;
      }
      tThemeDir = path+GUIGraphicsContext.ThemeName.Trim()+@"\";
      if (Directory.Exists(tThemeDir))
      {
        return tThemeDir;
      }
      return string.Empty;
    }

    #region Properties
    internal static void AddProperty(ref Hashtable Properties, string property, string value, ref ArrayList al, bool Now = false, bool AddToCache = true)
    {
      try
      {
        if (string.IsNullOrEmpty(value))
        {
          value = string.Empty;
        }
        if (Now)
        {
          SetProperty(property, value);
        }

        if (Properties.Contains(property))
        {
          Properties[property] = value;
        }
        else
        {
          Properties.Add(property, value);
        }

        if (!AddToCache)
        {
          return;
        }
        AddPictureToCache(property, value, ref al);
      }
      catch (Exception ex)
      {
        logger.Error("AddProperty: " + ex);
      }
    }

    internal static void UpdateProperties(ref Hashtable Properties)
    {
      try
      {
        if (Properties == null)
        {
          return;
        }

        foreach (DictionaryEntry dictionaryEntry in Properties)
        {
          SetProperty(dictionaryEntry.Key.ToString(), dictionaryEntry.Value.ToString());
        }
        Properties.Clear();
      }
      catch (Exception ex)
      {
        logger.Error("UpdateProperties: " + ex);
      }
    }

    internal static void SetProperty(string property, string value)
    {
      if (string.IsNullOrEmpty(property))
      {
        return;
      }
      if (property.IndexOf('#') == -1)
      {
        property = FanartHandlerPrefix + property;
      }

      try
      {
        // logger.Debug("*** SetProperty: "+property+" -> "+value) ;
        GUIPropertyManager.SetProperty(property, value);
      }
      catch (Exception ex)
      {
        logger.Error("SetProperty: " + ex);
      }
    }

    internal static string GetProperty(string property)
    {
      string result = string.Empty;
      if (string.IsNullOrEmpty(property))
      {
        return result;
      }
      if (property.IndexOf('#') == -1)
      {
        property = FanartHandlerPrefix + property;
      }

      try
      {
        result = GUIPropertyManager.GetProperty(property);
        if (string.IsNullOrEmpty(result))
        {
          result = string.Empty;
        }

        result = result.Trim();
        if (result.Equals(property, StringComparison.CurrentCultureIgnoreCase))
        {
          result = string.Empty;
        }
        // logger.Debug("*** GetProperty: "+property+" -> "+value) ;
        return result;
      }
      catch (Exception ex)
      {
        logger.Error("GetProperty: " + ex);
      }
      return string.Empty;
    }
    #endregion

    public static bool GetBool(string value)
    {
      if (string.IsNullOrEmpty(value))
      { 
        return false;
      }
      else
      {
        return (value.Equals("true", StringComparison.CurrentCultureIgnoreCase) || value.Equals("yes", StringComparison.CurrentCultureIgnoreCase));
      }
    }

    public static bool Contains(this string source, string toCheck, StringComparison comp)
    {
      return source.IndexOf(toCheck, comp) >= 0;
    }

    public static bool Contains(this string source, string toCheck, bool useRegex)
    {
      if (useRegex)
      {
        return Regex.IsMatch(source, @"\b" + toCheck + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ;
      }
      else
        return source.Contains(toCheck, StringComparison.OrdinalIgnoreCase);
    }

    #region ConainsID - Hashtable contains WindowID 
    public static bool ContainsID(Hashtable ht)
    {
      try
      {
        if (iActiveWindow > (int)GUIWindow.Window.WINDOW_INVALID)
        {
          return ContainsID(ht, sActiveWindow);
        }
      }
      catch { }
      return false;
    }

    public static bool ContainsID(Hashtable ht, Utils.Logo logoType)
    {
      try
      {
        if (iActiveWindow > (int)GUIWindow.Window.WINDOW_INVALID)
        {
          return ContainsID(ht, sActiveWindow + logoType.ToString());
        }
      }
      catch { }
      return false;
    }

    public static bool ContainsID(Hashtable ht, int iStr)
    {
      try
      {
        return ContainsID(ht, string.Empty + iStr);
      }
      catch { }
      return false;
    }

    public static bool ContainsID(Hashtable ht, string sStr)
    {
      return (ht != null) && (ht.ContainsKey(sStr));
    }
    #endregion

    #region Percent for progressbar
    public static int Percent(int Value, int Max)
    {
      return (Max > 0) ? Convert.ToInt32((Value*100)/Max) : 0 ;
    }

    public static int Percent(double Value, double Max)
    {
      return (Max > 0.0) ? Convert.ToInt32((Value*100.0)/Max) : 0 ;
    }
    #endregion

    #region Check [x]|[ ] for Log file
    public static string Check(bool Value, bool Box = true)
    {
      return (Box ? "[" : string.Empty) + (Value ? "x" : " ") + (Box ? "]" : string.Empty) ;
    }
    #endregion

    #region Get Awards
    public static string GetAwards(bool fromGenre)
    {
      string sAwardsValue = string.Empty;

      if (fromGenre && !AddAwardsToGenre)
      {
        return sAwardsValue; 
      }

      if (AwardsList != null)
      {
        string currentProperty = string.Empty;
        string currentPropertyValue = string.Empty;

        foreach (KeyValuePair<string, object> pair in AwardsList)
        {
          if (sActiveWindow.Equals(pair.Key.ToString()))
          {
            var _award = (Awards) pair.Value;
            if (!currentProperty.Equals(_award.Property))
            {
              currentProperty = _award.Property;
              currentPropertyValue = GetProperty(currentProperty); 
            }
            if (!string.IsNullOrEmpty(currentPropertyValue))
            {
              if (Regex.IsMatch(currentPropertyValue, _award.Regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
              {
                sAwardsValue = sAwardsValue + (string.IsNullOrEmpty(sAwardsValue) ? "" : "|") + _award.Name;
              }
            }
          }
        }
      }
      return sAwardsValue;
    }
    #endregion

    #region Get Genres and Studios
    public static string GetGenre(string sGenre)
    {
      if (string.IsNullOrEmpty(sGenre))
      {
        return string.Empty;
      }

      if (Genres != null && Genres.Count > 0)
      {
        var _genre = sGenre.ToLower(CultureInfo.InvariantCulture).RemoveDiacritics().RemoveSpacesAndDashs();
        if (Genres.ContainsKey(_genre))
        {
          return (string) Genres[_genre];
        }
      }
      return sGenre;
    }

    public static string GetCharacters(string sLine)
    {
      if (string.IsNullOrEmpty(sLine) || Characters == null)
      {
        return string.Empty;
      }

      string result = string.Empty;
      foreach (DictionaryEntry value in Characters)
      {
        try
        {
          // logger.Debug("*** " + sLine.ToLower(CultureInfo.InvariantCulture).RemoveDiacritics() + " - " + value.Key.ToString());
          if (sLine.ToLower(CultureInfo.InvariantCulture).RemoveDiacritics().Contains(value.Key.ToString(), true))
          {
            result = result + (string.IsNullOrEmpty(result) ? "" : "|") + value.Value; 
          }
        }
        catch { }
      }
      return result;
    }

    public static string GetCharacter(string sCharacter)
    {
      if (string.IsNullOrEmpty(sCharacter))
      {
        return string.Empty;
      }

      if (Characters != null && Characters.Count > 0)
      {
        var _character = sCharacter.ToLower(CultureInfo.InvariantCulture).RemoveDiacritics().Trim();
        if (Characters.ContainsKey(_character))
        {
          return (string) Characters[_character];
        }
      }
      return sCharacter;
    }

    public static string GetStudio(string sStudio)
    {
      if (string.IsNullOrEmpty(sStudio))
      {
        return string.Empty;
      }

      if (Studios != null && Studios.Count > 0)
      {
        var _studio = sStudio.ToLower(CultureInfo.InvariantCulture).RemoveDiacritics().RemoveSpacesAndDashs();
        if (Studios.ContainsKey(_studio))
        {
          return (string) Studios[_studio];
        }
      }
      return sStudio;
    }
    #endregion

    #region Settings
    public static void LoadAwardsNames()
    {
      AwardsList = new List<KeyValuePair<string, object>>();

      try
      {
        string FullFileName = Config.GetFile((Config.Dir) 10, ConfigAwardsFilename);
        if (!File.Exists(FullFileName))
        {
          return;
        }

        logger.Debug("Load Awards from file: {0}", ConfigAwardsFilename);

        XmlDocument doc = new XmlDocument();
        doc.Load(FullFileName);

        if (doc.DocumentElement != null)
        {
          XmlNodeList awardsList = doc.DocumentElement.SelectNodes("/awards");
          
          if (awardsList == null)
          {
            logger.Debug("Awards tag for file: {0} not exist. Skipped.", ConfigAwardsFilename);
            return;
          }

          foreach (XmlNode nodeAwards in awardsList)
          {
            if (nodeAwards != null)
            {
              // Awards settings
              XmlNode settings = nodeAwards.SelectSingleNode("settings");
              if (settings != null)
              {
                XmlNode nodeAddAwards = settings.SelectSingleNode("addawardstogenre");
                if (nodeAddAwards != null && nodeAddAwards.InnerText != null)
                {
                  string addAwards = nodeAddAwards.InnerText;
                  if (!string.IsNullOrEmpty(addAwards))
                  {
                    AddAwardsToGenre = GetBool(addAwards);
                  }
                }
              }

              // Awards
              XmlNodeList awardList = nodeAwards.SelectNodes("award");
              foreach (XmlNode nodeAward in awardList)
              {
                if (nodeAward != null)
                {
                  string awardName = string.Empty;
                  string awardWinID = string.Empty;
                  string awardProperty = string.Empty;
                  string awardRegex = string.Empty;

                  XmlNode nodeAwardName = nodeAward.SelectSingleNode("awardName");
                  if (nodeAwardName != null && nodeAwardName.InnerText != null)
                  {
                    awardName = nodeAwardName.InnerText;
                  }

                  XmlNodeList awardRuleList = nodeAward.SelectNodes("rule");
                  foreach (XmlNode nodeAwardRule in awardRuleList)
                  {
                    if (nodeAwardRule != null)
                    {
                      XmlNode nodeAwardWinID = nodeAwardRule.SelectSingleNode("winID");
                      XmlNode nodeAwardProperty = nodeAwardRule.SelectSingleNode("searchProperty");
                      XmlNode nodeAwardRegex = nodeAwardRule.SelectSingleNode("searchRegex");

                      if (nodeAwardWinID != null && nodeAwardWinID.InnerText != null)
                      {
                        awardWinID = nodeAwardWinID.InnerText;
                      }
                      if (nodeAwardProperty != null && nodeAwardProperty.InnerText != null)
                      {
                        awardProperty = nodeAwardProperty.InnerText;
                      }
                      if (nodeAwardRegex != null && nodeAwardRegex.InnerText != null)
                      {
                        awardRegex = nodeAwardRegex.InnerText;
                      }

                      if (!string.IsNullOrEmpty(awardName) && !string.IsNullOrEmpty(awardWinID) && !string.IsNullOrEmpty(awardProperty) && !string.IsNullOrEmpty(awardRegex))
                      {
                        // Add Award to Awards list
                        AddAwardToList(awardName, awardWinID, awardProperty, awardRegex);
                      }
                    }
                  }
                }
              }
            }
          }
          // Summary
          logger.Debug("Load Awards from file: {0} complete. {2}{1} loaded.", ConfigAwardsFilename, AwardsList.Count, Check(AddAwardsToGenre));
        }
      }
      catch (Exception ex)
      {
        Log.Error("LoadAwardsNames: Error loading genres from file: {0} - {1} ", ConfigAwardsFilename, ex.Message);
      }
    }

    public static void LoadGenresNames()
    {
      Genres = new Hashtable();
      try
      {
        string FullFileName = Config.GetFile((Config.Dir) 10, ConfigGenresFilename);
        if (!File.Exists(FullFileName))
        {
          return;
        }

        logger.Debug("Load Genres from file: {0}", ConfigGenresFilename);

        XmlDocument doc = new XmlDocument();
        doc.Load(FullFileName);

        if (doc.DocumentElement != null)
        {
          XmlNodeList genresList = doc.DocumentElement.SelectNodes("/genres");
          
          if (genresList == null)
          {
            logger.Debug("Genres tag for file: {0} not exist. Skipped.", ConfigGenresFilename);
            return;
          }

          foreach (XmlNode nodeGenres in genresList)
          {
            if (nodeGenres != null)
            {
              XmlNodeList genreList = nodeGenres.SelectNodes("genre");
              foreach (XmlNode nodeGenre in genreList)
              {
                if (nodeGenre != null && nodeGenre.Attributes != null && nodeGenre.InnerText != null)
                {
                  string name = nodeGenre.Attributes["name"].Value;
                  string genre = nodeGenre.InnerText;
                  if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(genre))
                  {
                    name = name.ToLower(CultureInfo.InvariantCulture).RemoveDiacritics().RemoveSpacesAndDashs();
                    genre = genre.Trim();
                    if (!Genres.Contains(name))
                    {
                      Genres.Add (name, genre);
                      // logger.Debug("*** Genre loaded: {0}/{1}", name, genre);
                    }
                  }
                }
              }
            }
          }
          logger.Debug("Load Genres from file: {0} complete. {1} loaded.", ConfigGenresFilename, Genres.Count);
        }
      }
      catch (Exception ex)
      {
        Log.Error("LoadGenresNames: Error loading genres from file: {0} - {1} ", ConfigGenresFilename, ex.Message);
      }
    }

    public static void LoadCharactersNames()
    {
      Characters = new Hashtable();
      try
      {
        string FullFileName = Config.GetFile((Config.Dir) 10, ConfigCharactersFilename);
        if (!File.Exists(FullFileName))
        {
          return;
        }

        logger.Debug("Load Characters from file: {0}", ConfigCharactersFilename);

        XmlDocument doc = new XmlDocument();
        doc.Load(FullFileName);

        if (doc.DocumentElement != null)
        {
          XmlNodeList charactersList = doc.DocumentElement.SelectNodes("/characters");
          
          if (charactersList == null)
          {
            logger.Debug("Characters tag for file: {0} not exist. Skipped.", ConfigCharactersFilename);
            return;
          }

          foreach (XmlNode nodeCharacters in charactersList)
          {
            if (nodeCharacters != null)
            {
              XmlNodeList characterList = nodeCharacters.SelectNodes("character");
              foreach (XmlNode nodeCharacter in characterList)
              {
                if (nodeCharacter != null && nodeCharacter.Attributes != null && nodeCharacter.InnerText != null)
                {
                  string name = nodeCharacter.Attributes["name"].Value;
                  string character = nodeCharacter.InnerText;
                  if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(character))
                  {
                    name = name.ToLower(CultureInfo.InvariantCulture).RemoveDiacritics().Trim();
                    character = character.Trim();
                    if (!Characters.Contains(name))
                    {
                      Characters.Add(name, character);
                      // logger.Debug("*** Character loaded: {0}/{1}", name, character);
                    }
                  }
                }
              }
            }
          }
          logger.Debug("Load Characters from file: {0} complete. {1} loaded.", ConfigCharactersFilename, Characters.Count);
        }
      }
      catch (Exception ex)
      {
        Log.Error("LoadCharactersNames: Error loading characters from file: {0} - {1} ", ConfigCharactersFilename, ex.Message);
      }

      List<string> charFolders = new List<string>();
      if (Directory.Exists(GUIGraphicsContext.Theme + FAHCharacters))
      {
        charFolders.Add(GUIGraphicsContext.Theme + FAHCharacters);
      }
      if (Directory.Exists(GUIGraphicsContext.Skin + FAHCharacters))
      {
        charFolders.Add(GUIGraphicsContext.Skin + FAHCharacters);
      }
      if (Directory.Exists(FAHFolder + FAHCharacters))
      {
        charFolders.Add(FAHFolder + FAHCharacters);
      }

      foreach (string charFolder in charFolders)
      {
        try
        {
          logger.Debug("Load Characters from folder: {0}", FAHCharacters);
          var files = new DirectoryInfo(charFolder).GetFiles("*.png");
          foreach (var fileInfo in files)
          {
            string fname = RemoveExtension(GetFileName(fileInfo.Name));
            string name = fname.ToLower(CultureInfo.InvariantCulture).RemoveDiacritics().Trim();
            if (!Characters.Contains(name))
            {
              Characters.Add(name, fname);
              // logger.Debug("*** Character loaded: {0}/{1}", name, fname);
            }
          }
          logger.Debug("Load Characters from folder: {0} complete. Total: {1} loaded.", FAHCharacters, Characters.Count);
        }
        catch (Exception ex)
        {
          Log.Error("LoadCharactersNames: Error loading characters from folder: {0} - {1} ", FAHCharacters, ex.Message);
        }
      }
    }

    public static void LoadStudiosNames()
    {
      Studios = new Hashtable();
      try
      {
        string FullFileName = Config.GetFile((Config.Dir) 10, ConfigStudiosFilename);
        if (!File.Exists(FullFileName))
        {
          return;
        }

        logger.Debug("Load Studios from file: {0}", ConfigStudiosFilename);
        XmlDocument doc = new XmlDocument();
        doc.Load(FullFileName);

        if (doc.DocumentElement != null)
        {
          XmlNodeList studiosList = doc.DocumentElement.SelectNodes("/studios");
          
          if (studiosList == null)
          {
            logger.Debug("Studios tag for file: {0} not exist. Skipped.", ConfigStudiosFilename);
            return;
          }

          foreach (XmlNode nodeStudios in studiosList)
          {
            if (nodeStudios != null)
            {
              XmlNodeList studioList = nodeStudios.SelectNodes("studio");
              foreach (XmlNode nodeStudio in studioList)
              {
                if (nodeStudio != null && nodeStudio.Attributes != null && nodeStudio.InnerText != null)
                {
                  string name = nodeStudio.Attributes["name"].Value;
                  string studio = nodeStudio.InnerText;
                  if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(studio))
                  {
                    name = name.ToLower(CultureInfo.InvariantCulture).RemoveDiacritics().RemoveSpacesAndDashs();
                    studio = studio.Trim();
                    if (!Studios.Contains(name))
                    {
                      Studios.Add (name, studio);
                      // logger.Debug("*** Studio loaded: {0}/{1}", name, studio);
                    }
                  }
                }
              }
            }
          }
          logger.Debug("Load Studios from file: {0} complete. {1} loaded.", ConfigStudiosFilename, Studios.Count);
        }
      }
      catch (Exception ex)
      {
        Log.Error("LoadStudiosNames: Error loading studios from file: {0} - {1} ", ConfigStudiosFilename, ex.Message);
      }
    }

    public static void LoadBadArtists()
    {
      try
      {
        BadArtistsList = new List<string>();
        logger.Debug("Load Artists from: " + ConfigBadArtistsFilename);
        string FullFileName = Config.GetFile((Config.Dir) 10, ConfigBadArtistsFilename);
        if (!File.Exists(FullFileName))
        {
          logger.Debug("Load Artists from: " + ConfigBadArtistsFilename + " failed, file not found.");
          return;
        }
        using (var xmlreader = new Settings(FullFileName))
        {
          int MaximumShares = 250;
          for (int index = 0; index < MaximumShares; index++)
          {
            string Artist = String.Format("artist{0}", index);
            string ArtistData = xmlreader.GetValueAsString("Artists", Artist, string.Empty);
            if (!string.IsNullOrEmpty(ArtistData) && (ArtistData.IndexOf("|") > 0) && (ArtistData.IndexOf("|") < ArtistData.Length))
            {
              var Left  = ArtistData.Substring(0, ArtistData.IndexOf("|")).ToLower().Trim();
              var Right = ArtistData.Substring(checked (ArtistData.IndexOf("|") + 1)).ToLower().Trim();
              // logger.Debug("*** "+ArtistData+" "+Left+" -> "+Right);
              BadArtistsList.Add(Left+"|"+Right) ;
            }
          }
        }
        logger.Debug("Load Artists from: "+ConfigBadArtistsFilename+" complete.");
      }
      catch (Exception ex)
      {
        logger.Error("LoadBadArtists: "+ex);
      }
    }

    public static void LoadMyPicturesSlideShowFolders()
    {
      if (!UseMyPicturesSlideShow)
        return;  

      try
      {
        MyPicturesSlideShowFolders = new List<string>();
        logger.Debug("Load MyPictures Slide Show Folders from: " + ConfigBadMyPicturesSlideShowFilename);
        string FullFileName = Config.GetFile((Config.Dir) 10, ConfigBadMyPicturesSlideShowFilename);
        if (!File.Exists(FullFileName))
        {
          logger.Debug("Load MyPictures Slide Show Folders from: " + ConfigBadMyPicturesSlideShowFilename + " failed, file not found.");
          return;
        }
        using (var xmlreader = new Settings(FullFileName))
        {
          int MaximumShares = 250;
          for (int index = 0; index < MaximumShares; index++)
          {
            string MyPicturesSlideShowFolder = String.Format("folder{0}", index);
            string MyPicturesSlideShowData = xmlreader.GetValueAsString("MyPicturesSlideShowFolders", MyPicturesSlideShowFolder, string.Empty);
            if (!string.IsNullOrEmpty(MyPicturesSlideShowData))
            {
              MyPicturesSlideShowFolders.Add(MyPicturesSlideShowData) ;
            }
          }
        }
        logger.Debug("Load MyPictures Slide Show Folders from: "+ConfigBadMyPicturesSlideShowFilename+" complete.");
      }
      catch (Exception ex)
      {
        logger.Error("LoadMyPicturesSlideShowFolders: "+ex);
      }
    }

    public static void SaveMyPicturesSlideShowFolders()
    {
      if (!UseMyPicturesSlideShow)
        return;  

      try
      {
        logger.Debug("Save MyPictures Slide Show Folders to: " + ConfigBadMyPicturesSlideShowFilename);
        string FullFileName = Config.GetFile((Config.Dir) 10, ConfigBadMyPicturesSlideShowFilename);
        using (var xmlwriter = new Settings(FullFileName))
        {
          int MaximumShares = 250;
          for (int index = 0; index < MaximumShares; index++)
          {
            string MyPicturesSlideShowFolder = String.Format("folder{0}", index);
            string MyPicturesSlideShowData = xmlwriter.GetValueAsString("MyPicturesSlideShowFolders", MyPicturesSlideShowFolder, string.Empty);
            if (!string.IsNullOrEmpty(MyPicturesSlideShowData))
            {
              xmlwriter.SetValue("MyPicturesSlideShowFolders", MyPicturesSlideShowFolder, string.Empty);
            }
          }
          int i = 0;
          foreach (var folder in MyPicturesSlideShowFolders)
          {
            string MyPicturesSlideShowFolder = String.Format("folder{0}", i);
            if (!string.IsNullOrEmpty(folder))
            {
              xmlwriter.SetValue("MyPicturesSlideShowFolders", MyPicturesSlideShowFolder, folder);
              i++;
            }
          }
        }
        logger.Debug("Save MyPictures Slide Show Folders to: "+ConfigBadMyPicturesSlideShowFilename+" complete.");
      }
      catch (Exception ex)
      {
        logger.Error("SaveMyPicturesSlideShowFolders: "+ex);
      }
    }

    public static void LoadSeparators(Settings xmlreader)
    {
      try
      {
        logger.Debug("Load Separators from: "+ConfigFilename);
        int MaximumShares = 250;
        for (int index = 0; index < MaximumShares; index++)
        {
          string Separator = String.Format("sep{0}", index);
          string SeparatorData = xmlreader.GetValueAsString("Separators", Separator, string.Empty);
          if (!string.IsNullOrEmpty(SeparatorData))
          {
            Array.Resize(ref PipesArray, PipesArray.Length + 1);
            PipesArray[PipesArray.Length - 1] = SeparatorData ;
          }
        }
        logger.Debug("Load Separators from: "+ConfigFilename+" complete.");
      }
      catch (Exception ex)
      {
        logger.Error("LoadSeparators: "+ex);
      }
    }

    public static void CreateDirectoryIfMissing(string directory)
    {
      if (!Directory.Exists(directory))
        Directory.CreateDirectory(directory);
    }

    public static void SetupDirectories()
    {
      try
      {
        CreateDirectoryIfMissing(FAHUDGames);
        CreateDirectoryIfMissing(FAHUDMovies);
        CreateDirectoryIfMissing(FAHUDMusic);
        CreateDirectoryIfMissing(FAHUDMusicAlbum);
        // CreateDirectoryIfMissing(FAHUDMusicGenre);
        CreateDirectoryIfMissing(FAHUDPictures);
        CreateDirectoryIfMissing(FAHUDScorecenter);
        CreateDirectoryIfMissing(FAHUDTV);
        CreateDirectoryIfMissing(FAHUDPlugins);
        CreateDirectoryIfMissing(FAHSMovies);
        CreateDirectoryIfMissing(FAHSMusic);
      }
      catch (Exception ex)
      {
        logger.Error("SetupDirectories: " + ex);
      }
    }

    public static void LoadSettings()
    {
      #region Init variables
      UseFanart = true;
      UseAlbum = true;
      UseArtist = true;
      SkipWhenHighResAvailable = true;
      DisableMPTumbsForRandom = true;
      ImageInterval = "30";
      MinResolution = "500x500";
      ScraperMaxImages = "3";
      ScraperMusicPlaying = true;
      ScraperMPDatabase = true;
      ScraperInterval = "12";
      UseAspectRatio = true;
      ScrapeThumbnails = true;
      ScrapeThumbnailsAlbum = true;
      DoNotReplaceExistingThumbs = true;
      UseGenreFanart = false;
      ScanMusicFoldersForFanart = false;
      MusicFoldersArtistAlbumRegex = string.Empty;
      UseOverlayFanart = false;
      UseMusicFanart = true;
      UseVideoFanart = true;
      UsePicturesFanart = true;
      UseScoreCenterFanart = true;
      DefaultBackdrop = string.Empty;
      DefaultBackdropMask = "*.jpg";
      DefaultBackdropIsImage = false;
      UseDefaultBackdrop = true;
      UseSelectedMusicFanart = true;
      UseSelectedOtherFanart = true;
      FanartTVPersonalAPIKey = string.Empty;
      DeleteMissing = false;
      UseHighDefThumbnails = false;
      UseMinimumResolutionForDownload = false;
      ShowDummyItems = false;
      AddAdditionalSeparators = false;
      UseMyPicturesSlideShow = false;
      FastScanMyPicturesSlideShow = false;
      LimitNumberFanart = 10;
      #endregion
      #region Init Providers
      UseFanartTV = true;
      UseHtBackdrops = true;
      UseLastFM = true;
      UseCoverArtArchive = true;
      #endregion
      #region Fanart.TV
      MusicClearArtDownload = true ;
      MusicBannerDownload = true;
      MusicCDArtDownload = true;
      MoviesClearArtDownload = true;
      MoviesBannerDownload = true;
      MoviesCDArtDownload = true;
      MoviesClearLogoDownload = true;
      MoviesFanartNameAsMediaportal = false;
      FanartTVLanguage = string.Empty ;
      FanartTVLanguageDef = "en" ;
      FanartTVLanguageToAny = false ;
      //
      PipesArray = new string[2] { "|", ";" };
      #endregion
      
      #region Internal
      MinWResolution = 0.0;
      MinHResolution = 0.0;
      #endregion

      try
      {
        logger.Debug("Load settings from: "+ConfigFilename);
        #region Load settings
        using (var settings = new Settings(Config.GetFile((Config.Dir) 10, ConfigFilename)))
        {
          UpgradeSettings(settings);
          //
          UseFanart = settings.GetValueAsBool("FanartHandler", "UseFanart", UseFanart);
          UseAlbum = settings.GetValueAsBool("FanartHandler", "UseAlbum", UseAlbum);
          UseArtist = settings.GetValueAsBool("FanartHandler", "UseArtist", UseArtist);
          SkipWhenHighResAvailable = settings.GetValueAsBool("FanartHandler", "SkipWhenHighResAvailable", SkipWhenHighResAvailable);
          DisableMPTumbsForRandom = settings.GetValueAsBool("FanartHandler", "DisableMPTumbsForRandom", DisableMPTumbsForRandom);
          ImageInterval = settings.GetValueAsString("FanartHandler", "ImageInterval", ImageInterval);
          MinResolution = settings.GetValueAsString("FanartHandler", "MinResolution", MinResolution);
          ScraperMaxImages = settings.GetValueAsString("FanartHandler", "ScraperMaxImages", ScraperMaxImages);
          ScraperMusicPlaying = settings.GetValueAsBool("FanartHandler", "ScraperMusicPlaying", ScraperMusicPlaying);
          ScraperMPDatabase = settings.GetValueAsBool("FanartHandler", "ScraperMPDatabase", ScraperMPDatabase);
          ScraperInterval = settings.GetValueAsString("FanartHandler", "ScraperInterval", ScraperInterval);
          UseAspectRatio = settings.GetValueAsBool("FanartHandler", "UseAspectRatio", UseAspectRatio);
          ScrapeThumbnails = settings.GetValueAsBool("FanartHandler", "ScrapeThumbnails", ScrapeThumbnails);
          ScrapeThumbnailsAlbum = settings.GetValueAsBool("FanartHandler", "ScrapeThumbnailsAlbum", ScrapeThumbnailsAlbum);
          DoNotReplaceExistingThumbs = settings.GetValueAsBool("FanartHandler", "DoNotReplaceExistingThumbs", DoNotReplaceExistingThumbs);
          UseGenreFanart = settings.GetValueAsBool("FanartHandler", "UseGenreFanart", UseGenreFanart);
          ScanMusicFoldersForFanart = settings.GetValueAsBool("FanartHandler", "ScanMusicFoldersForFanart", ScanMusicFoldersForFanart);
          MusicFoldersArtistAlbumRegex = settings.GetValueAsString("FanartHandler", "MusicFoldersArtistAlbumRegex", MusicFoldersArtistAlbumRegex);
          // UseOverlayFanart = settings.GetValueAsBool("FanartHandler", "UseOverlayFanart", UseOverlayFanart);
          // UseMusicFanart = settings.GetValueAsBool("FanartHandler", "UseMusicFanart", UseMusicFanart);
          // UseVideoFanart = settings.GetValueAsBool("FanartHandler", "UseVideoFanart", UseVideoFanart);
          // UsePicturesFanart = settings.GetValueAsBool("FanartHandler", "UsePicturesFanart", UsePicturesFanart);
          // UseScoreCenterFanart = settings.GetValueAsBool("FanartHandler", "UseScoreCenterFanart", UseScoreCenterFanart);
          // DefaultBackdrop = settings.GetValueAsString("FanartHandler", "DefaultBackdrop", DefaultBackdrop);
          DefaultBackdropMask = settings.GetValueAsString("FanartHandler", "DefaultBackdropMask", DefaultBackdropMask);
          // DefaultBackdropIsImage = settings.GetValueAsBool("FanartHandler", "DefaultBackdropIsImage", DefaultBackdropIsImage);
          UseDefaultBackdrop = settings.GetValueAsBool("FanartHandler", "UseDefaultBackdrop", UseDefaultBackdrop);
          UseSelectedMusicFanart = settings.GetValueAsBool("FanartHandler", "UseSelectedMusicFanart", UseSelectedMusicFanart);
          UseSelectedOtherFanart = settings.GetValueAsBool("FanartHandler", "UseSelectedOtherFanart", UseSelectedOtherFanart);
          FanartTVPersonalAPIKey = settings.GetValueAsString("FanartHandler", "FanartTVPersonalAPIKey", FanartTVPersonalAPIKey);
          DeleteMissing = settings.GetValueAsBool("FanartHandler", "DeleteMissing", DeleteMissing);
          UseHighDefThumbnails = settings.GetValueAsBool("FanartHandler", "UseHighDefThumbnails", UseHighDefThumbnails);
          UseMinimumResolutionForDownload = settings.GetValueAsBool("FanartHandler", "UseMinimumResolutionForDownload", UseMinimumResolutionForDownload);
          ShowDummyItems = settings.GetValueAsBool("FanartHandler", "ShowDummyItems", ShowDummyItems);
          UseMyPicturesSlideShow = settings.GetValueAsBool("FanartHandler", "UseMyPicturesSlideShow", UseMyPicturesSlideShow);
          FastScanMyPicturesSlideShow = settings.GetValueAsBool("FanartHandler", "FastScanMyPicturesSlideShow", FastScanMyPicturesSlideShow);
          //
          UseFanartTV = settings.GetValueAsBool("Providers", "UseFanartTV", UseFanartTV);
          UseHtBackdrops = settings.GetValueAsBool("Providers", "UseHtBackdrops", UseHtBackdrops);
          UseLastFM = settings.GetValueAsBool("Providers", "UseLastFM", UseLastFM);
          UseCoverArtArchive = settings.GetValueAsBool("Providers", "UseCoverArtArchive", UseCoverArtArchive);
          //
          AddAdditionalSeparators = settings.GetValueAsBool("Scraper", "AddAdditionalSeparators", AddAdditionalSeparators);
          //
          MusicClearArtDownload = settings.GetValueAsBool("FanartTV", "MusicClearArtDownload", MusicClearArtDownload);
          MusicBannerDownload = settings.GetValueAsBool("FanartTV", "MusicBannerDownload", MusicBannerDownload);
          MusicCDArtDownload = settings.GetValueAsBool("FanartTV", "MusicCDArtDownload", MusicCDArtDownload);
          MoviesClearArtDownload = settings.GetValueAsBool("FanartTV", "MoviesClearArtDownload", MoviesClearArtDownload);
          MoviesBannerDownload = settings.GetValueAsBool("FanartTV", "MoviesBannerDownload", MoviesBannerDownload);
          MoviesCDArtDownload = settings.GetValueAsBool("FanartTV", "MoviesCDArtDownload", MoviesCDArtDownload);
          MoviesClearLogoDownload = settings.GetValueAsBool("FanartTV", "MoviesClearLogoDownload", MoviesClearLogoDownload);
          MoviesFanartNameAsMediaportal = settings.GetValueAsBool("FanartTV", "MoviesFanartNameAsMediaportal", MoviesFanartNameAsMediaportal);
          //
          FanartTVLanguage = settings.GetValueAsString("FanartTV", "FanartTVLanguage", FanartTVLanguage);
          FanartTVLanguageToAny = settings.GetValueAsBool("FanartTV", "FanartTVLanguageToAny", FanartTVLanguageToAny);
          //
          if (AddAdditionalSeparators)
          {
            LoadSeparators (settings) ;
          }
        }
        #endregion
        logger.Debug("Load settings from: "+ConfigFilename+" complete.");
      }
      catch (Exception ex)
      {
        logger.Error("LoadSettings: "+ex);
      }
      //
      LoadAwardsNames();
      LoadGenresNames();
      LoadStudiosNames();
      LoadCharactersNames();
      LoadBadArtists();
      LoadMyPicturesSlideShowFolders();
      //
      #region Check Settings
      DefaultBackdrop = (string.IsNullOrEmpty(DefaultBackdrop) ? FAHUDMusic : DefaultBackdrop);
      if ((string.IsNullOrEmpty(MusicFoldersArtistAlbumRegex)) || (MusicFoldersArtistAlbumRegex.IndexOf("?<artist>") < 0) || (MusicFoldersArtistAlbumRegex.IndexOf("?<album>") < 0))
      {
        ScanMusicFoldersForFanart = false;
      }
      //
      FanartTVPersonalAPIKey = FanartTVPersonalAPIKey.Trim();
      MaxRefreshTickCount = checked (Convert.ToInt32(ImageInterval, CultureInfo.CurrentCulture) * (1000 / refreshTimerInterval));
      ScrapperTimerInterval = checked (Convert.ToInt32(ScraperInterval, CultureInfo.CurrentCulture) * ScrapperTimerInterval);
      //
      try
      {
        MinWResolution = (double) Convert.ToInt32(MinResolution.Substring(0, MinResolution.IndexOf("x", StringComparison.CurrentCulture)), CultureInfo.CurrentCulture);
        MinHResolution = (double) Convert.ToInt32(MinResolution.Substring(checked (MinResolution.IndexOf("x", StringComparison.CurrentCulture) + 1)), CultureInfo.CurrentCulture);
      }
      catch
      {
        MinResolution = "0x0";
        MinWResolution = 0.0;
        MinHResolution = 0.0;
      }
      #endregion
      //
      #region Report Settings
      logger.Info("Fanart Handler is using: " + Check(UseFanart) + " Fanart, " + Check(UseAlbum) + " Album Thumbs, " + Check(UseArtist) + " Artist Thumbs, " + Check(UseGenreFanart) + " Genre Fanart, Min: " + MinResolution + ", " + Check(UseAspectRatio) + " Aspect Ratio >= 1.3");
      logger.Debug("Scan: " + Check(ScanMusicFoldersForFanart) + " Music Folders for Fanart, RegExp: " + MusicFoldersArtistAlbumRegex);
      logger.Debug("Scraper: [x] Fanart, " + Check(ScraperMPDatabase) + " MP Databases , " + Check(ScrapeThumbnails) + " Artists Thumb , " + Check(ScrapeThumbnailsAlbum) + " Album Thumb, " + Check(UseMinimumResolutionForDownload) + " Delete if less then " + MinResolution + ", " + Check(UseHighDefThumbnails) + " High Def Thumbs, Max Count [" + ScraperMaxImages + "]");
      logger.Debug("Providers: " + Check(UseFanartTV) + " Fanart.TV, " + Check(UseHtBackdrops) + " HtBackdrops, " + Check(UseLastFM) + " Last.fm, " + Check(UseCoverArtArchive) + " CoverArtArchive");
      if (UseFanartTV)
      {
        logger.Debug("Fanart.TV: Language: [" + (string.IsNullOrEmpty(FanartTVLanguage) ? "Any]" : FanartTVLanguage + "] If not found, try to use Any language: " + FanartTVLanguageToAny));
        logger.Debug("Fanart.TV: Music: " + Check(MusicClearArtDownload) + " ClearArt, " + Check(MusicBannerDownload) + " Banner, " + Check(MusicCDArtDownload) + " CD");
        logger.Debug("Fanart.TV: Movie: " + Check(MoviesClearArtDownload) + " ClearArt, " + Check(MoviesBannerDownload) + " Banner, " + Check(MoviesCDArtDownload) + " CD, " + Check(MoviesClearLogoDownload) + " ClearLogo");
      }
      logger.Debug("Artists pipes: [" + string.Join("][", PipesArray) + "]");
      #endregion
    }

    public static void SaveSettings()
    {
      SaveMyPicturesSlideShowFolders();
      //
      try
      {
        logger.Debug("Save settings to: " + ConfigFilename);
        #region Save settings
        using (var xmlwriter = new Settings(Config.GetFile((Config.Dir) 10, ConfigFilename)))
        {
          xmlwriter.SetValueAsBool("FanartHandler", "UseFanart", UseFanart);
          xmlwriter.SetValueAsBool("FanartHandler", "UseAlbum", UseAlbum);
          xmlwriter.SetValueAsBool("FanartHandler", "UseArtist", UseArtist);
          xmlwriter.SetValueAsBool("FanartHandler", "SkipWhenHighResAvailable", SkipWhenHighResAvailable);
          xmlwriter.SetValueAsBool("FanartHandler", "DisableMPTumbsForRandom", DisableMPTumbsForRandom);
          xmlwriter.SetValueAsBool("FanartHandler", "UseSelectedMusicFanart", UseSelectedMusicFanart);
          xmlwriter.SetValueAsBool("FanartHandler", "UseSelectedOtherFanart", UseSelectedOtherFanart);
          xmlwriter.SetValue("FanartHandler", "ImageInterval", ImageInterval);
          xmlwriter.SetValue("FanartHandler", "MinResolution", MinResolution);
          xmlwriter.SetValue("FanartHandler", "ScraperMaxImages", ScraperMaxImages);
          xmlwriter.SetValueAsBool("FanartHandler", "ScraperMusicPlaying", ScraperMusicPlaying);
          xmlwriter.SetValueAsBool("FanartHandler", "ScraperMPDatabase", ScraperMPDatabase);
          xmlwriter.SetValue("FanartHandler", "ScraperInterval", ScraperInterval);
          xmlwriter.SetValueAsBool("FanartHandler", "UseAspectRatio", UseAspectRatio);
          xmlwriter.SetValueAsBool("FanartHandler", "ScrapeThumbnails", ScrapeThumbnails);
          xmlwriter.SetValueAsBool("FanartHandler", "ScrapeThumbnailsAlbum", ScrapeThumbnailsAlbum);
          xmlwriter.SetValueAsBool("FanartHandler", "DoNotReplaceExistingThumbs", DoNotReplaceExistingThumbs);
          xmlwriter.SetValueAsBool("FanartHandler", "UseDefaultBackdrop", UseDefaultBackdrop);
          xmlwriter.SetValue("FanartHandler", "DefaultBackdropMask", DefaultBackdropMask);
          xmlwriter.SetValueAsBool("FanartHandler", "UseGenreFanart", UseGenreFanart);
          xmlwriter.SetValueAsBool("FanartHandler", "ScanMusicFoldersForFanart", ScanMusicFoldersForFanart);
          xmlwriter.SetValue("FanartHandler", "MusicFoldersArtistAlbumRegex", MusicFoldersArtistAlbumRegex);
          xmlwriter.SetValue("FanartHandler", "FanartTVPersonalAPIKey", FanartTVPersonalAPIKey);
          xmlwriter.SetValueAsBool("FanartHandler", "DeleteMissing", DeleteMissing);
          xmlwriter.SetValueAsBool("FanartHandler", "UseHighDefThumbnails", UseHighDefThumbnails);
          xmlwriter.SetValueAsBool("FanartHandler", "UseMinimumResolutionForDownload", UseMinimumResolutionForDownload);
          xmlwriter.SetValueAsBool("FanartHandler", "ShowDummyItems", ShowDummyItems);
          xmlwriter.SetValueAsBool("FanartHandler", "UseMyPicturesSlideShow", UseMyPicturesSlideShow);
          // xmlwriter.SetValueAsBool("FanartHandler", "FastScanMyPicturesSlideShow", FastScanMyPicturesSlideShow);
          //
          xmlwriter.SetValueAsBool("Providers", "UseFanartTV", UseFanartTV);
          xmlwriter.SetValueAsBool("Providers", "UseHtBackdrops", UseHtBackdrops);
          xmlwriter.SetValueAsBool("Providers", "UseLastFM", UseLastFM);
          xmlwriter.SetValueAsBool("Providers", "UseCoverArtArchive", UseCoverArtArchive);
          //
          xmlwriter.SetValueAsBool("Scraper", "AddAdditionalSeparators", AddAdditionalSeparators);
          //
          xmlwriter.SetValueAsBool("FanartTV", "MusicClearArtDownload", MusicClearArtDownload);
          xmlwriter.SetValueAsBool("FanartTV", "MusicBannerDownload", MusicBannerDownload);
          xmlwriter.SetValueAsBool("FanartTV", "MusicCDArtDownload", MusicCDArtDownload);
          xmlwriter.SetValueAsBool("FanartTV", "MoviesClearArtDownload", MoviesClearArtDownload);
          xmlwriter.SetValueAsBool("FanartTV", "MoviesBannerDownload", MoviesBannerDownload);
          xmlwriter.SetValueAsBool("FanartTV", "MoviesCDArtDownload", MoviesCDArtDownload);
          xmlwriter.SetValueAsBool("FanartTV", "MoviesClearLogoDownload", MoviesClearLogoDownload);
          // xmlwriter.SetValueAsBool("FanartTV", "MoviesFanartNameAsMediaportal", MoviesFanartNameAsMediaportal);
          //
          xmlwriter.SetValue("FanartTV", "FanartTVLanguage", FanartTVLanguage);
          xmlwriter.SetValueAsBool("FanartTV", "FanartTVLanguageToAny", FanartTVLanguageToAny);
          //
        } 
        #endregion
        /*
        try
        {
          xmlwriter.SaveCache();
        }
        catch
        {   }
        */
        logger.Debug("Save settings to: " + ConfigFilename + " complete.");
      }
      catch (Exception ex)
      {
        logger.Error("SaveSettings: " + ex);
      }
    }

    public static void UpgradeSettings(Settings xmlwriter)
    {
      #region Init temp Variables
      var u_UseFanart = string.Empty;
      var u_UseAlbum = string.Empty;
      var u_UseArtist = string.Empty;
      var u_SkipWhenHighResAvailable = string.Empty;
      var u_DisableMPTumbsForRandom = string.Empty;
      var u_ImageInterval = string.Empty;
      var u_MinResolution = string.Empty;
      var u_ScraperMaxImages = string.Empty;
      var u_ScraperMusicPlaying = string.Empty;
      var u_ScraperMPDatabase = string.Empty;
      var u_ScraperInterval = string.Empty;
      var u_UseAspectRatio = string.Empty;
      var u_ScrapeThumbnails = string.Empty;
      var u_ScrapeThumbnailsAlbum = string.Empty;
      var u_DoNotReplaceExistingThumbs = string.Empty;
      var u_UseSelectedMusicFanart = string.Empty;
      var u_UseSelectedOtherFanart = string.Empty;
      var u_UseGenreFanart = string.Empty;
      var u_ScanMusicFoldersForFanart = string.Empty;
      var u_UseDefaultBackdrop = string.Empty;
      var u_AddAdditionalSeparators = string.Empty;
      var u_Separators = string.Empty ;

      #endregion
      try
      {
        logger.Debug("Upgrade settings file: " + ConfigFilename);
        #region Read Old Entry
        try
        {
          u_UseFanart = xmlwriter.GetValueAsString("FanartHandler", "useFanart", string.Empty);
          u_UseAlbum = xmlwriter.GetValueAsString("FanartHandler", "useAlbum", string.Empty);
          u_UseArtist = xmlwriter.GetValueAsString("FanartHandler", "useArtist", string.Empty);
          u_SkipWhenHighResAvailable = xmlwriter.GetValueAsString("FanartHandler", "skipWhenHighResAvailable", string.Empty);
          u_DisableMPTumbsForRandom = xmlwriter.GetValueAsString("FanartHandler", "disableMPTumbsForRandom", string.Empty);
          u_ImageInterval = xmlwriter.GetValueAsString("FanartHandler", "imageInterval", string.Empty);
          u_MinResolution = xmlwriter.GetValueAsString("FanartHandler", "minResolution", string.Empty);
          u_ScraperMaxImages = xmlwriter.GetValueAsString("FanartHandler", "scraperMaxImages", string.Empty);
          u_ScraperMusicPlaying = xmlwriter.GetValueAsString("FanartHandler", "scraperMusicPlaying", string.Empty);
          u_ScraperMPDatabase = xmlwriter.GetValueAsString("FanartHandler", "scraperMPDatabase", string.Empty);
          u_ScraperInterval = xmlwriter.GetValueAsString("FanartHandler", "scraperInterval", string.Empty);
          u_UseAspectRatio = xmlwriter.GetValueAsString("FanartHandler", "useAspectRatio", string.Empty);
          u_ScrapeThumbnails = xmlwriter.GetValueAsString("FanartHandler", "scrapeThumbnails", string.Empty);
          u_ScrapeThumbnailsAlbum = xmlwriter.GetValueAsString("FanartHandler", "scrapeThumbnailsAlbum", string.Empty);
          u_DoNotReplaceExistingThumbs = xmlwriter.GetValueAsString("FanartHandler", "doNotReplaceExistingThumbs", string.Empty);
          u_UseSelectedMusicFanart = xmlwriter.GetValueAsString("FanartHandler", "useSelectedMusicFanart", string.Empty);
          u_UseSelectedOtherFanart = xmlwriter.GetValueAsString("FanartHandler", "useSelectedOtherFanart", string.Empty);
          u_UseDefaultBackdrop = xmlwriter.GetValueAsString("FanartHandler", "useDefaultBackdrop", string.Empty);
          u_UseGenreFanart = xmlwriter.GetValueAsString("FanartHandler", "UseGenreFanart", string.Empty);
          u_ScanMusicFoldersForFanart = xmlwriter.GetValueAsString("FanartHandler", "ScanMusicFoldersForFanart", string.Empty);
          //
          u_AddAdditionalSeparators = xmlwriter.GetValueAsString("Scraper", "AndSignAsSeparator", string.Empty);
          u_Separators = xmlwriter.GetValueAsString("Separators", "sep0", string.Empty);
        }
        catch
        {   }
        #endregion
        #region Write New Entry
        if (!string.IsNullOrEmpty(u_UseFanart))
          xmlwriter.SetValue("FanartHandler", "UseFanart", u_UseFanart.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_UseAlbum))
          xmlwriter.SetValue("FanartHandler", "UseAlbum", u_UseAlbum.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_UseArtist))
          xmlwriter.SetValue("FanartHandler", "UseArtist", u_UseArtist.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_SkipWhenHighResAvailable))
          xmlwriter.SetValue("FanartHandler", "SkipWhenHighResAvailable", u_SkipWhenHighResAvailable.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_DisableMPTumbsForRandom))
          xmlwriter.SetValue("FanartHandler", "DisableMPTumbsForRandom", u_DisableMPTumbsForRandom.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_UseSelectedMusicFanart))
          xmlwriter.SetValue("FanartHandler", "UseSelectedMusicFanart", u_UseSelectedMusicFanart.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_UseSelectedOtherFanart))
          xmlwriter.SetValue("FanartHandler", "UseSelectedOtherFanart", u_UseSelectedOtherFanart.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_ImageInterval))
          xmlwriter.SetValue("FanartHandler", "ImageInterval", u_ImageInterval);
        if (!string.IsNullOrEmpty(u_MinResolution))
          xmlwriter.SetValue("FanartHandler", "MinResolution", u_MinResolution);
        if (!string.IsNullOrEmpty(u_ScraperMaxImages))
          xmlwriter.SetValue("FanartHandler", "ScraperMaxImages", u_ScraperMaxImages);
        if (!string.IsNullOrEmpty(u_ScraperMusicPlaying))
          xmlwriter.SetValue("FanartHandler", "ScraperMusicPlaying", u_ScraperMusicPlaying.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_ScraperMPDatabase))
          xmlwriter.SetValue("FanartHandler", "ScraperMPDatabase", u_ScraperMPDatabase.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_ScraperInterval))
          xmlwriter.SetValue("FanartHandler", "ScraperInterval", u_ScraperInterval.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_UseAspectRatio))
          xmlwriter.SetValue("FanartHandler", "UseAspectRatio", u_UseAspectRatio.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_ScrapeThumbnails))
          xmlwriter.SetValue("FanartHandler", "ScrapeThumbnails", u_ScrapeThumbnails.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_ScrapeThumbnailsAlbum))
          xmlwriter.SetValue("FanartHandler", "ScrapeThumbnailsAlbum", u_ScrapeThumbnailsAlbum.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_DoNotReplaceExistingThumbs))
          xmlwriter.SetValue("FanartHandler", "DoNotReplaceExistingThumbs", u_DoNotReplaceExistingThumbs.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_UseDefaultBackdrop))
          xmlwriter.SetValue("FanartHandler", "UseDefaultBackdrop", u_UseDefaultBackdrop.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_UseGenreFanart))
          xmlwriter.SetValue("FanartHandler", "UseGenreFanart", u_UseGenreFanart.Replace("True","yes").Replace("False","no"));
        if (!string.IsNullOrEmpty(u_ScanMusicFoldersForFanart))
          xmlwriter.SetValue("FanartHandler", "ScanMusicFoldersForFanart", u_ScanMusicFoldersForFanart.Replace("True","yes").Replace("False","no"));
        //
        if (!string.IsNullOrEmpty(u_AddAdditionalSeparators))
          xmlwriter.SetValue("Scraper", "AddAdditionalSeparators", u_AddAdditionalSeparators);
        #endregion
        #region Delete old Entry
        try
        {
          xmlwriter.RemoveEntry("FanartHandler", "useFanart");
          xmlwriter.RemoveEntry("FanartHandler", "useAlbum");
          xmlwriter.RemoveEntry("FanartHandler", "useArtist");
          xmlwriter.RemoveEntry("FanartHandler", "skipWhenHighResAvailable");
          xmlwriter.RemoveEntry("FanartHandler", "disableMPTumbsForRandom");
          xmlwriter.RemoveEntry("FanartHandler", "useSelectedMusicFanart");
          xmlwriter.RemoveEntry("FanartHandler", "useSelectedOtherFanart");
          xmlwriter.RemoveEntry("FanartHandler", "imageInterval");
          xmlwriter.RemoveEntry("FanartHandler", "minResolution");
          xmlwriter.RemoveEntry("FanartHandler", "scraperMaxImages");
          xmlwriter.RemoveEntry("FanartHandler", "scraperMusicPlaying");
          xmlwriter.RemoveEntry("FanartHandler", "scraperMPDatabase");
          xmlwriter.RemoveEntry("FanartHandler", "scraperInterval");
          xmlwriter.RemoveEntry("FanartHandler", "useAspectRatio");
          xmlwriter.RemoveEntry("FanartHandler", "scrapeThumbnails");
          xmlwriter.RemoveEntry("FanartHandler", "scrapeThumbnailsAlbum");
          xmlwriter.RemoveEntry("FanartHandler", "doNotReplaceExistingThumbs");
          xmlwriter.RemoveEntry("FanartHandler", "useDefaultBackdrop");

          xmlwriter.RemoveEntry("FanartHandler", "latestPictures");
          xmlwriter.RemoveEntry("FanartHandler", "latestMusic");
          xmlwriter.RemoveEntry("FanartHandler", "latestMovingPictures");
          xmlwriter.RemoveEntry("FanartHandler", "latestTVSeries");
          xmlwriter.RemoveEntry("FanartHandler", "latestTVRecordings");
          xmlwriter.RemoveEntry("FanartHandler", "refreshDbPicture");
          xmlwriter.RemoveEntry("FanartHandler", "refreshDbMusic");
          xmlwriter.RemoveEntry("FanartHandler", "latestMovingPicturesWatched");
          xmlwriter.RemoveEntry("FanartHandler", "latestTVSeriesWatched");
          xmlwriter.RemoveEntry("FanartHandler", "latestTVRecordingsWatched");
        }
        catch
        {   }
        try
        {
          xmlwriter.RemoveEntry("Scraper", "AndSignAsSeparator");
        }
        catch
        {   }
        try
        {
          int MaximumShares = 250;
          for (int index = 0; index < MaximumShares; index++)
          {
            xmlwriter.RemoveEntry("Artists", String.Format("artist{0}", index));
          }
          // xmlwriter.RemoveSection("Artists");
        }
        catch
        {   }
        try
        {
          if (string.IsNullOrEmpty(u_Separators))
          {
            xmlwriter.SetValue("Separators", "sep0", " & ");
            xmlwriter.SetValue("Separators", "sep1", " feat ");
            xmlwriter.SetValue("Separators", "sep2", " feat. ");
            xmlwriter.SetValue("Separators", "sep3", " and ");
            xmlwriter.SetValue("Separators", "sep4", " и ");
            xmlwriter.SetValue("Separators", "sep5", " und ");
            xmlwriter.SetValue("Separators", "sep6", " et ");
            xmlwriter.SetValue("Separators", "sep7", ",");
            xmlwriter.SetValue("Separators", "sep8", " ft ");
          }
        }
        catch
        {   }
        #endregion
        /*
        try
        {
          xmlwriter.SaveCache();
        }
        catch
        {   }
        */
        logger.Debug("Upgrade settings file: " + ConfigFilename + " complete.");
      }
      catch (Exception ex)
      {
        logger.Error("UpgradeSettings: " + ex);
      }
    }
    #endregion

    #region Awards
    public static void AddAwardToList(string name, string wID, string property, string regex)
    {
      var award = new Awards();
      award.Name = name;
      award.Property = property;
      award.Regex = regex;

      KeyValuePair<string,object> myItem = new KeyValuePair<string,object>(wID, award);
      AwardsList.Add(myItem);
    }

    public class Awards
    {
      public string Name; 
      public string Property; 
      public string Regex; 
    }
    #endregion

    public enum Category
    {
      GameManual,
      MovieManual,
      MovieScraped,
      MovingPictureManual,
      MusicAlbumThumbScraped,
      MusicArtistThumbScraped,
      MusicFanartManual,
      MusicFanartScraped,
      MusicFanartAlbum,
      PictureManual,
      PluginManual,
      SportsManual,
      TvManual,
      TVSeriesManual,
      TvSeriesScraped,
      FanartTVArt, 
      FanartTVCDArt, 
      Dummy,
    }

    public enum Provider
    {
      HtBackdrops,
      LastFM, 
      FanartTV,
      MyVideos,
      MovingPictures,
      TVSeries,
      MyFilms,
      MusicFolder, 
      CoverArtArchive, 
      Local,
      Dummy, 
    }

    public enum Logo
    {
      Horizontal,
      Vertical,
    }
  }

  public static class ThreadSafeRandom
  {
    [ThreadStatic] private static Random Local;

    public static Random ThisThreadsRandom
    {
      get { return Local ?? (Local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId))); }
    }
  }
}
