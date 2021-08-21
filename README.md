# TwitterStreamWorker
Generates a stream of tweets from given hashtags. Bad words filter and block users included. Using tweetinvi. .NET 5.0 

# Installation
- Download source.
- Add appsettings.json with

[code]
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
    "TimerRandomMax": 100,
    "TimerRandomMin": 0,
    "TimerThreshold": 40
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}




[/code]
