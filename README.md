# TwitterStreamWorker
Generates a stream of tweets from given hashtags and publishes a retweet to the clients account. Bad words filter and block users included. Using tweetinvi. .NET 5.0 

# Installation
- Download source.
- Add appsettings.json with

```
{
  "Settings": {
    "APIKey": "",
    "APISecret": "",
    "AccessToken": "",
    "AccessSecret": "",
    "StreamTracks": [
      "TrackOne",
      "TrackTwo",
    ],
    "BadWords": [
      "badwordOne",
      "badwordTwo",
    ],
    "BlockedUsers": [
      "userid",
      "nextuserid"
    ],
    "ReTweetProfiles": [
        "TwitterId",
        "NextTwitterId"
    ]
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```
