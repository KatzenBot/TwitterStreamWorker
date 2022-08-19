using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Exceptions;
using Tweetinvi.Streaming;

namespace TwitterStreamWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly WorkerOptions _options;
        private TwitterClient _appClient;
        public List<long> PublishTweets { get; set; } = new List<long>();
        public List<long> TweetUsers { get; set; } = new List<long>();
        public Worker(ILogger<Worker> logger, WorkerOptions options)
        {
            _logger = logger;
            _options = options;
        }
        /// <summary>
        /// Execute WorkerService as a background service
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(">_ TwitterStreamWorker running at: {time}", DateTimeOffset.Now);
                await AuthTwitter(stoppingToken);
            }
        }
        /// <summary>
        /// WorkerOptions from appsettings.json
        /// </summary>
        public class WorkerOptions
        {
            public string APIKey { get; set; }
            public string APISecret { get; set; }
            public string AccessToken { get; set; }
            public string AccessSecret { get; set; }
            public string[] StreamTracks { get; set; }
            public string[] BadWords { get; set; }
            public long[] BlockedUsers { get; set; }
            public string[] ContentPublishing { get; set; }
            public int ContentTimeSpan { get; set;}
            public int RetweetTimeSpan { get;set;}
        }
        /// <summary>
        /// Authenticate via Twitter API - tweetinvi https://github.com/linvi/tweetinvi
        /// Start twitter stream if successful
        /// </summary>
        public async Task AuthTwitter(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation($">_ AuthTwitter started... " + DateTime.Now);
                _logger.LogInformation($">_ Trying to Authenticate... " + DateTime.Now);

                // Only show for debugging reasons
                //_logger.LogInformation($" " + _options.APIKey + "\n" + _options.APISecret + "\n" + _options.AccessToken + "\n" + _options.AccessSecret);

                // Auth
                // User client
                var appClient = new TwitterClient(_options.APIKey, _options.APISecret, _options.AccessToken, _options.AccessSecret);
                var authenticatedUser = await appClient.Users.GetAuthenticatedUserAsync();
                _logger.LogInformation($">_ LoggedIn as " + authenticatedUser.ScreenName + " " + DateTime.Now);
                _appClient = appClient;
            }
            catch (TwitterAuthException ex)
            {
                _logger.LogError(ex.Message);
            }
            catch (TwitterException ex)
            {
                _logger.LogError(ex.Message);
            }
            finally
            {
                await StartStreamAsync(_appClient, stoppingToken);
            }
        }
        /// <summary>
        /// Start twitter stream on all conditions and filter tweets to the publishing queue 
        /// 
        /// </summary>
        public async Task StartStreamAsync(TwitterClient _appClient, CancellationToken stoppingToken)
        {
            try
            {

                // Before executing request
                TweetinviEvents.BeforeExecutingRequest += (sender, args) =>
                {
                    // before executing
                };

                // Waiting for rate limits
                TweetinviEvents.WaitingForRateLimit += (sender, args) =>
                {
                    _logger.LogInformation($"\n >>> Waiting for rate limits... ");
                };

                // subscribe to application level events
                TweetinviEvents.BeforeExecutingRequest += (sender, args) =>
                {
                    // application level logging
                    _logger.LogInformation($"\n >>> Event: " + args.Url + "\n");
                };

                // For a client to be included in the application events you will need to subscribe to this client's events
                TweetinviEvents.SubscribeToClientEvents(_appClient); // Check if working


                // Create Stream
                var stream = _appClient.Streams.CreateFilteredStream();

                // This option allows the application to get notified 
                // if the stream is about to be disconnected
                stream.StallWarnings = true;

                //--------------------------------------------------------------------------------------------------------------------
                #region ======> KeepAliveReceived | StreamStarted | StreamResumed | StreamStopped | WarningFallingBehindDetected 

                stream.KeepAliveReceived += async (sender, args) =>
                {
                    _logger.LogWarning(">_ Keep alive received...");
                    await Task.CompletedTask.ConfigureAwait(true);
                };

                // Check if this is firing
                stream.StreamStarted += async (sender, args) =>
                {
                    _logger.LogWarning($">_ Stream started...");
                    await Task.CompletedTask.ConfigureAwait(true);
                };

                stream.StreamResumed += async (sender, args) =>
                {
                    _logger.LogWarning($">_ Stream resumed...");
                    await Task.CompletedTask.ConfigureAwait(true);
                };

                stream.StreamStopped += async (sender, args) =>
                {
                    var exceptionThatCausedTheStreamToStop = args.Exception;
                    var twitterDisconnectMessage = args.DisconnectMessage;

                    _logger.LogWarning(">_ {0} Stream stopped... " + stream.StreamState);
                    _logger.LogWarning(">_ {0} ", twitterDisconnectMessage);

                    await Task.Delay(1000, stoppingToken);
                };

                stream.WarningFallingBehindDetected += async (sender, args) =>
                {
                    _logger.LogWarning($">_ Warning falling behind...");
                    await Task.CompletedTask.ConfigureAwait(true);
                };

                stream.UnmanagedEventReceived += async (sender, args) =>
                {
                    _logger.LogWarning($">_ Unmanged Event...");
                    await Task.CompletedTask.ConfigureAwait(true);
                };

                stream.LimitReached += async (sender, args) =>
                {
                    _logger.LogWarning($">_ Limit reached...");
                    await Task.CompletedTask.ConfigureAwait(true);
                };

                stream.DisconnectMessageReceived += async (sender, args) =>
                {
                    _logger.LogWarning($">_ Stream disconnected...");
                    await Task.CompletedTask.ConfigureAwait(true);

                };

                #endregion
                //--------------------------------------------------------------------------------------------------------------------
                
                // => Start the Stream with the given settings
                
                // Getting stream tracks from settings
                var tracks = _options.StreamTracks.ToList();

                foreach (var track in tracks)
                {
                    // Add each track to the stream
                    //_logger.LogInformation($"" + track);
                    stream.AddTrack(track);
                }

                // Only match hashtag entities
                stream.MatchOn = MatchOn.HashTagEntities;

                //--------------------------------------------------------------------------------------------------------------------
                // Mainstream
                //

                // Loading bad words and blocked users from appSettings 
                _logger.LogInformation($">_ Loading bad Words from appsettings...");
                var badWords = _options.BadWords.ToList();
                _logger.LogInformation($">_ Loading blocked users from appsettings...");
                var blockedUsers = _options.BlockedUsers.ToList();

                // Hit when matching tweet is received
                stream.MatchingTweetReceived += async (sender, args) =>
                { 
                    if (args.MatchOn == stream.MatchOn)
                    { 
                        var tweet = args.Tweet;
                        string fulltext = tweet.FullText;
                        int hashtagCount = tweet.Hashtags.Count;
                        int mediaCount = tweet.Media.Count;

                        // #####################################
                        // !!! Change statements to a switch !!!
                        // #####################################

                        // Check if reply
                        if (tweet.InReplyToScreenName != null)
                        {
                            _logger.LogInformation($">_ Skipped because Tweet is a reply...");
                            return;
                        }

                        // Check for too many mentions
                        int mentionsCount = 0;
                        if (tweet.Entities.UserMentions.Count > 0)
                        {
                            //_logger.LogInformation($">_ Tweet has mentions...");
                            mentionsCount = tweet.Entities.UserMentions.Count;
                            //_logger.LogInformation($">_ Mentions: " + mentionsCount);
                        }

                        // Check if tweet has URL included
                        if (tweet.Entities.Urls.Count > 0)
                        {
                            _logger.LogInformation($">_ Skipped because url in tweet...");
                            return;
                        }

                        // Check if quoted tweet
                        if (tweet.QuotedTweet != null )
                        {
                            _logger.LogInformation($">_ Skipped because quoted tweet...");
                            return;
                        }

                        // Check if retweet
                        if (tweet.IsRetweet == true)
                        {
                            // Return if retweet
                            _logger.LogInformation($">_ Skipped because retweet...");
                            return;
                        }

                        // Check if mentions too high
                        if (mentionsCount > 1)
                        {
                            // Return if too many mentions
                            _logger.LogInformation($">_ Skipped because too many mentions...");
                            return;
                        }

                        // Check for hashtag count
                        if (hashtagCount > 3)
                        {
                            // Check for HashtagCount Limit
                            // Banned for hashtags
                            _logger.LogInformation($">_ Too many hashtags...");
                            return;
                        }

                        // Check if user already posted
                        if (TweetUsers.Contains(tweet.CreatedBy.Id) == true)
                        {
                            // Remove from Userlist after delay
                            await Task.Delay(TimeSpan.FromSeconds(60));
                            _logger.LogInformation(">_ Removed user from posting queue...");
                            TweetUsers.Remove(tweet.CreatedBy.Id);
                            return;
                        }

                        // Check if tweet has media attached
                        if (mediaCount < 1)
                        {
                            // Only tweet if media attached
                            // No Media attached
                            _logger.LogInformation($">_ No Media attached...");
                            return;
                        }
                        else
                        {
                            // Check for BadWords
                            foreach (var word in badWords)
                            {
                                if (fulltext.Contains(word))
                                {
                                    // Block user for using BadWord
                                    _logger.LogInformation($">_ Bad word found: " + word);
                                    //await _appClient.Users.BlockUserAsync(tweet.CreatedBy);
                                    return;
                                }
                            }
                            // Check for blocked user
                            foreach (var user in blockedUsers)
                            {
                                if (tweet.CreatedBy.Id == user)
                                {
                                    // return if blocked user is found
                                    _logger.LogInformation($">_ Blocked user found: " + user);
                                    return;
                                }
                            }
                            // Show Tweet debug details
                            _logger.LogInformation(
                                 "\n TweetId: " + tweet.Id
                                + "\n Date: " + tweet.CreatedAt
                                + "\n Author: " + tweet.CreatedBy
                                + "\n AuthorId: " + tweet.CreatedBy.Id
                                + "\n HashtagCount: " + tweet.Hashtags.Count
                                + "\n MediaCount: " + tweet.Media.Count
                                + "\n MentionsCount: " + mentionsCount
                                + "\n "
                                );
                            try
                            {
                                // Add user Id to Posting queue if not already in it
                                if (TweetUsers.Contains(tweet.CreatedBy.Id) != true)
                                {
                                    TweetUsers.Add(tweet.CreatedBy.Id);
                                }
                                else
                                {
                                    // Return is user already in posting queue
                                    return;
                                }
                                // Add Tweet to Publishing queue
                                // add tweet id to list
                                // check if list already contains id
                                if(PublishTweets.Contains(tweet.Id) != true)
                                {
                                    PublishTweets.Add(tweet.Id);
                                }

                                //await _appClient.Tweets.PublishRetweetAsync(tweet);
                                // Return, testing if necessary...
                                return;
                            }
                            catch (TwitterException ex)
                            {
                                if (ex.StatusCode == 403)
                                {
                                    _logger.LogInformation($"TwitterEx... " + ex.Message);
                                    PublishTweets.Remove(args.Tweet.Id);
                                }
                                else
                                {
                                    _logger.LogInformation($"TwitterEx... " + ex.Message);
                                    PublishTweets.Remove(args.Tweet.Id);
                                }
                            }
                        }
                    }
                    await Task.CompletedTask.ConfigureAwait(true);
                };

                // Publish Media in paralell task
                Parallel.Invoke(async () => await PublishMedia());

                // Content publishing in paralell 
                Parallel.Invoke(async () => await ContentPublishing());

                // Start first stream
                await stream.StartMatchingAllConditionsAsync().ConfigureAwait(true);
            }
            catch (ArgumentException ex)
            {
                _logger.LogInformation($"ArgumentEx... " + ex.Message);

                await Task.Delay(1000, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception... " + ex.Message);

                await Task.Delay(1000, stoppingToken);
            }
        }

        /// <summary>
        /// Publish Media Tweets with Ratelimits in a queue 
        /// </summary>
        public async Task PublishMedia()
        {
            _logger.LogInformation(">_ Media publishing is starting...");
            // If cattweets are null on startup wait for timeframe
            if (PublishTweets == null)
            {
                // Move timer value to appSettings
                await Task.Delay(TimeSpan.FromSeconds(120));
            }

            // endless loop service for publishing tweets
            while (PublishTweets.Count() != -1)
            {
                if (PublishTweets.Count() == 0)
                {
                    // Post content every Time queue hits 0 and wait for x seconds
                    // move to appSettings
                    await Task.Delay(TimeSpan.FromSeconds(120));
                }

                foreach (var tweet in PublishTweets.ToList())
                {
                    _logger.LogInformation(">_ Tweets in publishing queue: " + PublishTweets.Count());
                    _logger.LogInformation(">_ Users in posting queue: " + TweetUsers.Count());
                    // Timer for RateLimits
                    await Task.Delay(TimeSpan.FromSeconds(_options.RetweetTimeSpan));
                    try
                    {
                        // Publish Tweet
                        await _appClient.Tweets.PublishRetweetAsync(tweet);
                        _logger.LogInformation(">_ Posted Tweet with Id: " + tweet);
                        // Remove Tweet from queue
                        PublishTweets.Remove(tweet);
                    }
                    catch(Exception ex)
                    {
                        _logger.LogInformation(ex.Message);
                        PublishTweets.Remove(tweet);
                    }
                }
            }
        }

        /// <summary>
        /// ContentPublishing queue
        /// </summary>
        public async Task ContentPublishing()
        {
            _logger.LogInformation(">_ Content publishing is starting...");
            var contentTempList = _options.ContentPublishing;

            // Pretty simple shuffle
            var rnd = new Random();
            var contentList = contentTempList.OrderBy(item => rnd.Next());

            // Check if no content is listed in appSettings.json
            if (contentList == null)
            {
                _logger.LogCritical(">_ No content to publish...");
                // Move value to appSettings
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
             
            while(contentList.Count() != -1)
            {
                //Post content in timeframe
                //Shuffle the list 
                foreach(var tweet in contentList)
                {
                    try
                    {
                        _logger.LogInformation(">_ Waiting to publish new content...");
                        // Move value to appSettings
                        await Task.Delay(TimeSpan.FromMinutes(_options.ContentTimeSpan));
                        await _appClient.Tweets.PublishTweetAsync(tweet);
                        _logger.LogInformation(">_ Publish content: " + tweet);
                    }
                    catch(TwitterException ex)
                    {
                        _logger.LogCritical(ex.Message);
                    }
                }
            }
        }
        /// <summary>
        /// Timed Events - post special tweets on special times
        /// </summary>
        public async Task TimedEvents()
        {
            // Import timed events from JSON
            // If today in collection tweet it

            // Flag for already posted today

            // Check for todays timestamp



            await Task.Delay(5);

        }
    }
}