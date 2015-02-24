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
using Snowflake.Scraper;
using System.ComponentModel.Composition;
using Snowflake.Service;
using Snowflake.Constants;
using DuoVia.FuzzyStrings;
namespace Scraper.TheGamesDB
{
    public class TheGamesDB : BaseScraper
    {
        [ImportingConstructor]
        public TheGamesDB([Import("coreInstance")] ICoreService coreInstance)
            : base(Assembly.GetExecutingAssembly(), coreInstance)
        {
        }

        private IList<IGameScrapeResult> ParseSearchResults(Uri searchUri)
        {
            using (var client = new WebClient())
            {
                string xml = client.DownloadString(searchUri);
                XDocument xmlDoc = XDocument.Parse(xml);
                var games = (from game in xmlDoc.Descendants("Game") select game).ToList();
                var results = new List<IGameScrapeResult>();
                foreach (var game in games)
                {
                    string id = game.Element("id").Value;
                    string title = game.Element("GameTitle").Value;
                    string platform = "UNKNOWN";
                    string xmlPlatformValue = game.Element("Platform").Value;
                    if (this.ScraperMap.Reverse.ContainsKey(xmlPlatformValue)) platform = this.ScraperMap.Reverse[xmlPlatformValue];

                    results.Add(new GameScrapeResult(id, platform, title));
                }
                return results;
            }
        }
        public override IList<IGameScrapeResult> GetSearchResults(string searchQuery)
        {
            var searchUri = new Uri(Uri.EscapeUriString("http://thegamesdb.net/api/GetGamesList.php?name=" + searchQuery));
            var results = ParseSearchResults(searchUri);
            return results;
        }

        public override IList<IGameScrapeResult> GetSearchResults(string searchQuery, string platformId)
        {
            var searchUri = new Uri(Uri.EscapeUriString("http://thegamesdb.net/api/GetGamesList.php?name=" + searchQuery
                + "&platform=" + this.ScraperMap[platformId]));
            var results = ParseSearchResults(searchUri);
            return results;
        }
        public override IList<IGameScrapeResult> GetSearchResults(IDictionary<string, string> identifiedMetadata, string platformId)
        {
            if(identifiedMetadata.ContainsKey("Identifier-CMPDats"))
            {
                return this.GetSearchResults(identifiedMetadata["Identifier-CMPDats"], platformId);
            }
            else
            {
                return this.GetSearchResults(identifiedMetadata.Values.First(), platformId);
            }
        }
        public override IList<IGameScrapeResult> GetSearchResults(IDictionary<string, string> identifiedMetadata, string searchQuery, string platformId)
        {
            if (identifiedMetadata.ContainsKey("Identifier-CMPDats"))
            {
                return this.GetSearchResults(identifiedMetadata["Identifier-CMPDats"], platformId);
            }
            else
            {
                return this.GetSearchResults(searchQuery, platformId);
            }
        }
        public override IList<IGameScrapeResult> SortBestResults(IDictionary<string, string> identifiedMetadata, IList<IGameScrapeResult> searchResults)
        {
            string gameName = identifiedMetadata["Identifier-CMPDats"];
            return searchResults.OrderBy(result => result.GameTitle.LevenshteinDistance(gameName)).ToList();
        }

        public override Tuple<IDictionary<string, string>, IGameImagesResult> GetGameDetails(string id)
        {
            var searchUri = new Uri(Uri.EscapeUriString("http://thegamesdb.net/api/GetGame.php?id=" + id));
            using (var client = new WebClient())
            {
                string xml = client.DownloadString(searchUri);
                XDocument xmlDoc = XDocument.Parse(xml);
                string baseImageUrl = xmlDoc.Descendants("baseImgUrl").First().Value;
                var metadata = new Dictionary<string, string>
                {
                    {GameInfoFields.game_description, xmlDoc.Descendants("Overview").First().Value},
                    {GameInfoFields.game_title, xmlDoc.Descendants("GameTitle").First().Value},
                    {GameInfoFields.game_releasedate, xmlDoc.Descendants("ReleaseDate").First().Value},
                    {GameInfoFields.game_publisher, xmlDoc.Descendants("Publisher").First().Value},
                    {GameInfoFields.game_developer, xmlDoc.Descendants("Developer").First().Value}
                };

                IGameImagesResult images = new GameImagesResult();
                var boxartFront = baseImageUrl + (from boxart in xmlDoc.Descendants("boxart") where boxart.Attribute("side").Value == "front" select boxart).First().Value;
                images.AddFromUrl(GameImageType.IMAGE_BOXART_FRONT, new Uri(boxartFront));

                var boxartBack = baseImageUrl + (from boxart in xmlDoc.Descendants("boxart") where boxart.Attribute("side").Value == "back" select boxart).First().Value;
                images.AddFromUrl(GameImageType.IMAGE_BOXART_BACK, new Uri(boxartBack));

                //Add fanarts
                var fanarts = (from fanart in xmlDoc.Descendants("fanart") select fanart).ToList();
                foreach (string fanartUrl in fanarts.Select(fanart => baseImageUrl + fanart.Element("original").Value))
                {
                    images.AddFromUrl(GameImageType.IMAGE_FANART, new Uri(fanartUrl));
                }

                //Add screenshots
                var screenshots = (from screenshot in xmlDoc.Descendants("screenshot") select screenshot).ToList();
                foreach (string screenshotUrl in screenshots.Select(screenshot => baseImageUrl + screenshot.Element("original").Value))
                {
                    images.AddFromUrl(GameImageType.IMAGE_SCREENSHOT, new Uri(screenshotUrl));
                }
                return Tuple.Create<IDictionary<string, string>,IGameImagesResult>(metadata, images);
            }

        }
    }
}
