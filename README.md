# TwitterStreamWorker
Generates a stream of tweets from given hashtags and publishes a retweet to the clients account. Bad words filter and block users included.
- Using .NET 7.0
- Using tweetinvi
- Using SeriLog

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
    "ContentTimeSpan":  60,
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
    "ContentPublishing": [
        "",
        ""
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

See BOT in action: 
- https://twitter.com/UnitedSpaceCats
