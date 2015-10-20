using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Net;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Snowflake.Scraper;
using System.ComponentModel.Composition;
using Snowflake.Service;
using Snowflake.Constants;
using TheGamesDBAPI;
namespace Scraper.TheGamesDB
{
    public class TheGamesDbScraper : BaseScraper
    {
        [ImportingConstructor]
        public TheGamesDbScraper([Import("coreInstance")] ICoreService coreInstance)
            : base(Assembly.GetExecutingAssembly(), coreInstance)
        {
        }

        /// <summary>
        /// Removes spaces and punctuation from a string for Levenshtein comparison
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static string StripString(string input)
        {
            return Regex.Replace(input, @"[^a-zA-Z0-9]", "");
        }

        public override Tuple<IDictionary<string, string>, IGameImagesResult> GetGameDetails(IGameScrapeResult gameScrapeResult)
        {
            return this.GetGameDetails(gameScrapeResult.ID);
        }

        public override Tuple<IDictionary<string, string>, IGameImagesResult> GetGameDetails(string id)
        {
            var result = ApiGamesDb.GetGame(Convert.ToInt32(id));
            IGameImagesResult images = new GameImagesResult();
           
            images.AddFromUrl(GameImageType.IMAGE_BOXART_FRONT, new Uri(ApiGamesDb.BaseImgURL + result.Images.BoxartFront.Path));
            images.AddFromUrl(GameImageType.IMAGE_BOXART_BACK, new Uri(ApiGamesDb.BaseImgURL + result.Images.BoxartBack.Path));

            foreach (var fanart in result.Images.Fanart)
            {
                images.AddFromUrl(GameImageType.IMAGE_FANART, new Uri(ApiGamesDb.BaseImgURL + fanart.Path));
            }
            foreach (var screenshot in result.Images.Screenshots)
            {
                images.AddFromUrl(GameImageType.IMAGE_SCREENSHOT, new Uri(ApiGamesDb.BaseImgURL + screenshot.Path));
            }

            IDictionary<string, string> resultData = new Dictionary<string, string>();
            resultData[GameInfoFields.game_title] = result.Title;
            resultData[GameInfoFields.game_description] = result.Overview;
            resultData[GameInfoFields.game_developer] = result.Developer;
            resultData[GameInfoFields.game_publisher] = result.Publisher;
            resultData[GameInfoFields.game_releasedate] = result.ReleaseDate;
            return new Tuple<IDictionary<string, string>, IGameImagesResult>(resultData, images);

        }


        public override IList<IGameScrapeResult> GetSearchResults(IScrapableInfo scrapableInfo)
        {
            return this.GetSearchResults(scrapableInfo.QueryableTitle, scrapableInfo.StonePlatformId);
        }

        public override IList<IGameScrapeResult> GetSearchResults(string searchQuery)
        {
            return this.GetSearchResults(searchQuery, "");
        }

        public override IList<IGameScrapeResult> GetSearchResults(string searchQuery, string platformId)
        {
            string tgdbPlatform = this.ScraperMap[platformId];
            var results = ApiGamesDb.GetGames(searchQuery, tgdbPlatform);
            return results.Select(result =>
            new GameScrapeResult(result.ID.ToString(),
            this.ScraperMap.Reverse[result.Platform],
            result.Title,
            JaroWinklerDistance.proximity(searchQuery, result.Title),
            this.PluginName)).Cast<IGameScrapeResult>().ToList();
        }
    }
}
