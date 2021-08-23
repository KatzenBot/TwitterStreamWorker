using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
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
        public Worker(ILogger<Worker> logger, WorkerOptions options)
        {
            _logger = logger;
            _options = options;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(">_ TwitterStreamWorker running at: {time}", DateTimeOffset.Now);
                await AuthTwitter(stoppingToken);
            }
        }
        public class WorkerOptions
        {
            public string APIKey { get; set; }
            public string APISecret { get; set; }
            public string AccessToken { get; set; }
            public string AccessSecret { get; set; }

            public string[] StreamTracks { get; set; }
            public string[] BadWords { get; set; }
            public long[] BlockedUsers { get; set; }

            public int TimerRandomMax { get; set; }
            public int TimerRandomMin { get; set; }
            public int TimerThreshold { get; set; }
        }

        public async Task AuthTwitter(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation($">_ AuthTwitter started... " + DateTime.Now);
                _logger.LogInformation($">_ Gettings Settings from Database... " + DateTime.Now);

                // Get Settings from appSettings
                //Settings settings = await _appDBContext.Settings.FirstOrDefaultAsync(c => c.Id.Equals(1));

                _logger.LogInformation($">_ Trying to Authenticate... " + DateTime.Now);

                // Only show for debugging reasons
                //_logger.LogInformation($" " + _options.APIKey + "\n" + _options.APISecret + "\n" + _options.AccessToken + "\n" + _options.AccessSecret);
                // Auth
                // User client
                var appClient = new TwitterClient(_options.APIKey, _options.APISecret, _options.AccessToken, _options.AccessSecret);
                var authenticatedUser = await appClient.Users.GetAuthenticatedUserAsync();

                _logger.LogInformation($">_ LoggedIn as " + authenticatedUser.ScreenName + DateTime.Now);

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
        public async Task StartStreamAsync(TwitterClient _appClient, CancellationToken stoppingToken)
        {
            try
            {
                // Before executing request
                TweetinviEvents.BeforeExecutingRequest += (sender, args) =>
                {
                    // lets delay all operations from this client by 1 second
                    Task.Delay(TimeSpan.FromSeconds(1));
                };

                // Waiting for rate limits
                TweetinviEvents.WaitingForRateLimit += (sender, args) =>
                {
                    _logger.LogInformation($"\n >>> Waiting for rate limits... ");
                    Task.Delay(TimeSpan.FromHours(3));
                };

                // subscribe to application level events
                TweetinviEvents.BeforeExecutingRequest += (sender, args) =>
                {
                    // application level logging
                    _logger.LogInformation($"\n >>> Event: " + args.Url + "\n");
                };

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
                // Start the Stream with the Settings
                // 

                // Getting stream tracks from settings
                var tracks = _options.StreamTracks.ToList();

                foreach (var track in tracks)
                {
                    _logger.LogInformation($"" + track);

                    stream.AddTrack(track);
                }

                // Only match the addfollows
                stream.MatchOn = MatchOn.HashTagEntities;

                //--------------------------------------------------------------------------------------------------------------------
                // Mainstream
                //

                _logger.LogInformation($">_ Loading BadWords from settings...");
                var badWords = _options.BadWords.ToList();
                _logger.LogInformation($">_ Loading blocked users from settings...");
                var blockedUsers = _options.BlockedUsers.ToList();

                foreach (var word in badWords)
                {
                    _logger.LogInformation($"" + word);
                }

                // Hit when matching tweet is received
                stream.MatchingTweetReceived += async (sender, args) =>
                {
                    if (args.MatchOn == stream.MatchOn)
                    { 
                        var tweet = args.Tweet;
                        string fulltext = tweet.FullText;
                        int hashtagCount = tweet.Hashtags.Count;
                        int mediaCount = tweet.Media.Count;
                        int mentionsCount = 0;
                        if (tweet.InReplyToScreenName != null)
                        {
                            mentionsCount = tweet.InReplyToScreenName.Length;
                        }

                        var random = new Random();
                        var timerRandom = random.Next(_options.TimerRandomMin, _options.TimerRandomMax);

                        // Timerhack
                        if(timerRandom > _options.TimerThreshold)
                        {
                            // Return because of posting threshold
                            _logger.LogInformation($">_ Skipped because timer threshold hit..." + timerRandom);
                            return;
                        }
                        if (tweet.IsRetweet == true)
                        {
                            // Return if retweet
                            _logger.LogInformation($">_ Skipped because retweet...");
                            return;
                        }
                        if(mentionsCount > 1)
                        {
                            // Return if too many mentions
                            _logger.LogInformation($">_ Skipped because too many mentions...");
                            return;
                        }
                        if (hashtagCount > 3)
                        {
                            // Check for HashtagCount Limit
                            // Banned for hashtags
                            _logger.LogInformation($">_ Too many hashtags...");
                            return;
                        }
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
                                    //await _appClient.Users.BlockUserAsync(tweet.CreatedBy);
                                    return;
                                }
                            }
                            // Show Tweet debug details
                            _logger.LogInformation(
                                "\n"
                                + "\n TweetId: " + tweet.Id
                                +"\n Date: " + tweet.CreatedAt
                                + "\n Author: " + tweet.CreatedBy
                                + "\n AuthorId: " + tweet.CreatedBy.Id
                                + "\n"
                                + "\n Fulltext: "
                                + "\n"
                                + "\n"
                                + tweet.FullText
                                + "\n"
                                + "\n"
                                + "\n HashtagCount: " + tweet.Hashtags.Count
                                + "\n MediaCount: " + tweet.Media.Count
                                + "\n"
                                );

                            try
                            {
                                // Checking rate limits
                                var updateLimit = await _appClient.RateLimits.GetRateLimitsAsync();
                                var remainingRetweets = updateLimit.StatusesRetweetsIdLimit.Remaining;
                                _logger.LogInformation($">_ Remaing tweets:" + remainingRetweets.ToString());

                                if (remainingRetweets == 0)
                                {
                                    _logger.LogInformation($">_ Retweet not possible not enough remaining actions...");
                                    return;
                                }
                                else
                                {
                                    // Publish retweet and favorite Tweet
                                    _logger.LogInformation($">_ Publish retweet...");
                                    await _appClient.Tweets.PublishRetweetAsync(tweet);
                                }
                            }
                            catch (TwitterException ex)
                            {
                                _logger.LogInformation($"TwitterEx... " + ex.Message);
                            }
                        }
                    }
                    await Task.CompletedTask.ConfigureAwait(true);
                };
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
    }
}
