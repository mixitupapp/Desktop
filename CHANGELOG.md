# Mix It Up Desktop Changelog

## v1.3.0.5

* Adding $userbitslifetimeamount Special Identifier for pulling the total amount of bits cheered by a user from Twitch
* Fixing bug with YouTube-only connections preventing features such as Community Commands and Webhooks from working properly
* Fixing bug related to Twitch stream offline detection not working reliably
* Fixing bug with Tiltify donations not being processed correctly due to timestamp parsing issues
* Various quality of life & bug fixes

## v1.3.0.4

* Adding Record Clip option to Meld Studio action
* Improvements to connectivity speed for Twitch EventSub client connection on login
* Fixing bug with YouTube Live title & description updating not working properly
* Fixing bug with Twitch Bits $topbitscheered Special Identifiers not processing correctly
* Fixing bug with Crowd Control commands not triggering due to lack of effect quantities specified
* Various quality of life & bug fixes

## v1.3.0.0-1.3.0.3

* **BREAKING CHANGES:**

* New authentication data is needed for all Twitch & YouTube accounts. Streamer accounts will be prompted to re-authenticate to Twitch & YouTube when logging in, while bot accounts for Twitch & YouTube will need to be manually re-connected on the Accounts page
* Due to changes with Twitch's new APIs and the up-coming shutdown of their old APIs, some functionality in Mix It Up that Twitch has not migrated to the new APIs yet will become unsupported until they have added them back. Below is a list of features that are no longer usable:

* Twitch Channel Watch Streak event command is now disabled as user watch streak notifications are not sent out via the new Twitch APIs
* $usersubplanname Special Identifier for Twitch subscription-based event commands will no longer be the customized name of a channel's sub plan and instead will be the standard name (EX: Tier 1) as the new Twitch APIs do not include that information
* Changes to how Twitch bot accounts send messages & whispers in chat no longer require an active connection to your channel, which also means bot accounts no longer process **UNIQUE** data sent to them. The only feature this applied to was processing whispers sent directly to bot accounts, which is now currently not supported. If there is enough demand for this feature, we will investigate adding support for it in the future.

* Large-scale restructure and improvements to general Streaming Platform connectivity, allowing for easier connection & disconnection of streaming platforms
* New Twitch features:

* Event commands have been added for the following features: Highlighted Message, User Intro, Power-Up Message Effect, Power-Up Gigantified Emote, and Power-Up Celebration
* Migration to Twitch's new APIs, allowing for improved connectivity to Twitch's services and providing new functionality in future updates

* New YouTube features:

* Support for multiple, active live streams on the same YouTube account (EX: Regular stream & short)
* All currently tracked, active live streams are now displayed on the Channel page
* Adding the ability to manually connect to live streams if they are not automatically detected via the Channel page

* The Meld Studio action has been added:

* The Meld Studio action allows you control functionality within Meld Studio such as changing scenes, showing/hiding layers & effects, and more
* Meld Studio can be connected to by visiting the Services page of Mix It Up

* Fixing bug with $usersubmonths not using the proper value for the continuous number of sub months for Twitch re-subscriptions
* Fixing bug with Twitch re-subscription messages not being properly assigned to the $message Special Identifier
* Fixing bug with Text to Speech actions not working correctly when used with TTS.Monster
* Fixing bug with $streamgameimage not properly working for Twitch accounts
* Fixing bug with chat messages & whispers not working correctly in some cases when a bot account is signed in. This requires users to re-connect their bot account manually on the Accounts page.
* Various quality of life & bug fixes

## v1.2.0.11-12

* Adding support for Team Incentives and Team Milestones for DonorDrive. These are usable only while the "Include Team Donations / Events" option is toggled under Services -> DonorDrive
* Fixing bug with Twitch Mass Gifted Sub event command not triggering if the Mass Gifted Subs Filter Amount setting is disabled
* Various quality of life & bug fixes

## v1.2.0.0-1.2.0.10

* Overlay v3 functionality updates:

* **BREAKING CHANGES:**

* All Overlay Endpoints now share the same port number and instead have unique URLs associated with them. If you use more than 1 Overlay Endpoint, you will need to update the web browser sources in your streaming software to use the new URLs, which can be found under Settings -> Overlay.
* Overlay Widgets are now vastly more robust in the amount that can be customized for them. However, the differences in specific HTML formatting between old and new versions are incompatible and thus can not be directly migrated over. The vast majority of standard, drop-down or input values are converted over, but customized HTML can not be and is instead changed to the new format. All old customized HTML can be accessed from the Overlay Widgets page by editing the widget and clicking on the History button in the top-right corner of the window.
* Certain features and functionality have been removed due to their low usage and may be replaced in the future.
* The Web Page option has been removed due to low compatability with more websites, but web page can be added manually via the HTML option. Existing Web Page actions have been updated to this new setup via the HTML option.
* The Ticker Tape Overlay Widget has been removed and instead replaced with the comparable Label Overlay Widget. Due to their large differences, the Ticker Tape Overlay Widget was not migrated and must be manually re-created as a Label Overlay Widget.

* Overlay actions and widgets now have fully customizable HTML, CSS, and Javascript for users who are more adapt at web development
* Global HTML, CSS, and Javascript customization options can be set for Overlay Endpoints that will be present in all Overlay Actions & Overlay Widgets that use that Overlay Endpoint
* Adding "Run Widget Function" option to Overlay Action to allow for dynamic function triggering in Overlay Widgets
* Adding support for reference local files within an Overlay Action or Widget's code:

* By putting {LocalFile:\\FILE\_PATH} in your HTML, CSS, or Javascript, the Overlay service will handle automatically translating this to a local URL reference
* EX: {LocalFile:\\C:\Foo\Bar.png}
* EX: <img src="{LocalFile:\\C:\Foo\Bar.png}" />

* New Overlay Action Options:

* The Timer option allows for dynamic, non-interactable timers to be displayed on the Overlay
* The Twitch Clip option allows for the displaying of clips from a user stream. A known issue exists for some clips that been created witin the last month for users due to a major change Twitch performed in how clips are created and stored. Please up-vote the following User Voice to encourage Twitch to allow for this functionality: <https://twitch.uservoice.com/forums/310213-developers/suggestions/39228784-extend-clips-api-to-provide-the-mp4-url-so-editors>
* The Emote Effect option allow for dynamic displaying of emotes, emoticons, and images in a visual animation such as explosions and rain
* Several new options have been added for dynamically interacting with various Overlay Widget types

* Overlay Actions Upgrades:

* The Duration field for Overlay Actions now support Special Identifiers.
* Video and YouTube options now support not specifying a duration amount, which will play the entire length of the video
* New position option are available including the ability to have something display in a randomized location
* The same Overlay action will now no longer replace an old instance if it is still visible and instead will stack over it (EX: Triggering the same Overlay action 5 times in succession will now show 5 instances together rather than only 1).
* Image actions now can optionally use the native size of the image file instead of manually supplied a width and height if the fields are left blank.
* Overlay actions that use width and height now support size scaling if only a width or height is specified.
* Adding support for adding dynamic animations to Overlay Action visuals

* New Overlay Widgets:

* Label Overlay Widget

* The Label Overlay Widget allows you to display persistent information for your stream that will automatically update as changes occur.
* Common examples are for showing the latest follower, subscriber, donation, etc.
* Labels can be set up to automatically rotate through a selection of different options or show the latest one that has been updated/

* Persistent Timer Overlay Widget:

* The Persistent Timer Overlay Widget is a combination of the previous Timer and Timer Train Overlay Widgets.
* A displayable timer can be shown on the Overlay that will gradually count down.
* Various options on the widget can be set to add seconds to the timer or seconds can be added dynamically via the Overlay Action -> Add To Persistent Timer.

* Wheel Overlay Widget:

* The Wheel Overlay Widget allows you to present a visual prize wheel to your stream with various outcomes that can be randomly selected
* In addition to setting the probability on each outcome, you can also set a positive or negative modifier on each outcome to adjust the probability of the outcome

* If any outcome with a negative modifier is selected, the probabilities for all outcomes are adjusted by their modifier amount
* If any outcome with a positive modifier is selected, all outcomes are reset to their initial probabilities

* Each outcome can have a customized command for it that will be triggered when the outcome is selected or the default outcome command will be triggered instead if the outcome-specific command has no actions in it
* The Wheel Overlay Widget can be triggered dynamically via the Overlay Action -> Spin Wheel

* Persistent Emote Effect Overlay Widget:

* The Emote Effect allows you to apply a visual animation to emotes that occur in chat and your commands
* This overlay widget will automatically trigger the emote effect for all emotes or emojis detected in chat messages in your stream
* You can specify how many are shown on screen for each emote that is detected to increase the effect
* Combos can be enabled to ensure the emote effect will only trigger if a specific emote is detected a certain amount of times in chat messages over a certain period of seconds
* Duplicate emotes can also be ignored within the same chat message

* Poll Overlay Widget:

* The Poll Overlay Widget allows you to display visual progress of various multi-answer question systems used in Mix It Up. The following are supported:

* Twitch Polls
* Twitch Predictions
* Mix It Up Bet Game
* Mix It Up Trivia Game

* Custom Overlay Widget type:

* Custom Overlay Widgets have no pre-baked functionality and instead expose dedicated Javascript functions for when various different events occur in your stream.
* These are useful for when you want to build a more specific type of Overlay visual that does not conform to one of the standard Overlay Widget types.

* Overlay Widgets Upgrades:

* More detailed customization options are available for various widgets.
* A new Persistent Timer type is available which combines both of the features of the old Timer and Timer Train widgets.
* Overlay Widgets can now be directly assigned to a dedicated URL for that widget only.

* The Overlay as as service is now enabled by default for all new profiles.
* All Overlay Endpoints now share the exact same port number and instead have a unique URL associated with them.
* The Overlay port number can now be fully customized.

* Text to Speech Action updates:

* Windows Text To Speech added as a provider, leveraging your natively installed text to speech languages on your Windows computer
* TTS.Monster added as a provider, which can be enabled via the Services page after connecting your streaming account on their website: <https://tts.monster/>
* Amazon Polly and Microsoft Azure Speech added as providers:

* Amazon Polly & Microsoft Azure Speech both leverage a payment model for usage of their service and as a result, we have implemented a cooldown usage to them currently of 1 each every 5 minutes. This cooldown amount may be decreased or increased over time depending on the usage and cost associated with supporting these services.
* Users can set up their own Amazon Web Services (AWS) or Microsoft Azure account account and connect it on the Services page to remove the cooldown effect, however the linked account will then be billed for all usage of the service for Text to Speech

* TikTok TTS added as a provider. This service is hosted by Weilbyte at <https://tiktok-tts.weilbyte.dev> and may experience outages due to use.
* A new toggle option has been added that will block the Text to Speech action until the audio of the speech has completed for providers that support it.
* Adding SSML text processing support for Amazon Polly, Microsoft Azure Speech, and Windows Text to Speech providers for Text to Speech Action

* The Script action has been added:

* The Script action allows you to add code scripts to your commands that will be run when the action is triggered.
* Scripts can be written in either C#, Python, or Javascript.
* Special Identifier replacement is supported in the text of the script.
* Any value returned from the script is stored in the Special Identifier "$scriptresult" which can be leveraged in subsequent action.
* There is no unique functionality provided in the Script action to directly interact with Mix It Up, the Script action simply is a means to perform more complex logic in a simpler form for experience users. However, you are able to leverage the Mix It Up Developer API from inside your Script action to perform some operations within Mix It Up.
* Javascript-based scripts will run using the Mix It Up Overlay, allowing you to leverage external libraries by including them in the Overlay Endpoint via customization under Settings -> Overlay.
* Python-based scripts now require a local installation of Python on your PC by running directly via the Python interpreter. The Executable location for Python must be set under Settings -> Commands.

* The VTS P.O.G. action has been added:

* The VTS P.O.G. action allows you to interact with your instance of VTS P.O.G. to trigger functionality within the application
* The "Text To Speech" option allows you to trigger text to speech sound from a variety of providers supported within the application
* The "AI Text To Speech" option allows you to generate AI responses based on a specified prompt, which is then spoken through text to speech within the application
* The "Play Audio File" option allows you to specify a local audio file to be played via the TTS pet, AI pet, or soundboard within the application

* The Repeat action has been added:

* The Repeat action allows you to easily build a set of actions that will be repeatedly run in sequence based on the number of times you specify
* The amount of times the Repeat action runs can either take a raw number or a Special Identifier that translated to a number

* The Group action has been added:

* The Group action allows to easily group together a set of actions under a single "parent" action
* This can be useful in a few scenarios:

* Within the Random action to have sets of actions occur randomly instead single actions
* Easily being able to enable/disable a set of action
* Logical display and organization

* Adding the following YouTube-specific Special Identifiers

* $streamyoutubeid and $streamyoutubeurl Special Identifiers for the active YouTube Live stream
* $youtubelatestvideoid, $youtubelatestvideotitle, and $youtubelatestvideourl Special Identifiers for the latest non-stream video on the connected YouTube channel

* Adding support for automatic removal to VIP User option on the Twitch Action. This will remove VIP status from the user after the specified duration. This will only occur while Mix It Up is running and lapsed durations while Mix It Up is not running will be automatically handled when Mix It Up is next launched.
* Removing check for duplicate user accounts in settings and now allowing for multiple profiles to be created with the same user account. Profiles can now be specifically named by visiting Settings -> General.
* Viewers who have linked their accounts in Mix It Up across different streaming platforms will now properly show both accounts on the Users page when they are watching from both and the correct account information will be returned for commands triggered from each platform
* Adding "Persist No Duplicates" option to Random Action to keep track of what actions have already been triggered to ensure no action is run more than once. Once all actions have been triggered once, tracking is reset again to once per action.
* Adding "Is In" an "Is Not In" operators to Conditional Action for checking if a piece of text exists somewhere within a larger piece of text
* Adding "Read Each Line From File" option to File Action to support repeated running of a set of actions against each line in a file
* The Throw Item option on the Twitch Integrated Throwing System action now supports Special Identifiers for the amount to throw
* Adding Change Folder option to the Music Player Action
* Adding settings option to hide specific users messages from Chat view in Mix It Up
* Adding $usersubpoints Special Identifier to the Twitch Channel Subscribed, Twitch Channel Resubscribed, and Twitch Channel Gifted Subscription event commands to indicate the amount of Twitch Sub Points gained for the channel from the subscription
* Adding $substotalpoints Special Identifier to Twitch Channel Mass Gifted Subscriptions event command to indicate the total amount of Twitch Sub Points gained for the channel from all of the subscriptions gifted
* Adding $hypetrainlevel Special Identifier to Twitch Channel Hype Train Start event command
* Adding $gamequeueusersX Special Identifier to retrieve the top X many users in the Game Queue
* Adding $userlastseendays, $userlastseenage, and $userlastseendate Special Identifiers
* Adding $\_\_\_\_uniqueitemstotal Special Identifier for Inventories
* Adding $usernotes Special Identifier
* Adding option to command tester dialog for whether to use command locks or not when testing a command
* Adjusting Mix It Up update host & detection method to improve reliability
* Fixing bug with Tiltify service not properly handling team-based campaigns
* Fixing bug with BetterTTV animated emotes not doing animations on various Overlay types
* Fixing bug with Height property not getting properly assigned for Stream Boss Overlay Widget
* Fixing bug with End Credits Overlay Widget not properly processing Special Identifiers
* Fixing bug with HTML Overlay Widgets not properly refreshing their contents. If you have modified the Javascript at all of an HTML Overlay Widget, please create a new version and copy over your specific changes to the new updates.
* Adding "Random In", "Random Out", and "Random Visible" options for Overlay animations
* Fixing bug with "Run Widget Function" option for Overlay Action not properly processing Special Identifiers for values
* Updating Twitch Integrated Throwing System connection to background gather information to reduce loading times
* Adding User property to data variable in the update(data) Javascript function for the Label Overlay Widget
* Adding Followers and Subscribers types for Goal Overlay Widget to show automatic tracking of your current follower and subscriber amounts
* Adding Latest Subscription Gifter type to Label Overlay Widget
* Adding volume slider for wheel click sound to Wheel Overlay Widget
* Reducing the amount of libraries and external dependencies loaded in for the Overlay to increase loading speed and reduce the chance of batched Overlay actions drifting
* Adding the ability to custom the ≤head> tag for an Overlay Endpoint
* Changing Special Identifier for Time Adjusted sub-command on Persistent Timer Overlay Widget to be $timersecondsadjusted
* Fixing bug with Twitch multi-gifted subs doubling the amount of subs added for various Overlay Widgets such as Goal and Persistent Timer
* Adding error message for when the End Credits Overlay Widgets is triggered, but there is no captured data for it from when it was saved
* Fixing bug related several Overlay Widgets not working properly due to bad merge of changes
* Fixing bug with End Credits not showing data for Followers, Subscribers, and Moderators sections if there is not also a Chatters section added
* Fixing bug with LocalFile:\\ replacement function for Overlays not working properly
* Adding support for Woah.css animations for Overlay Actions and Widgets
* Various quality of life & bug fixes

## v1.1.0.17

* Adding mtion studio service connectivity:

* mtion studio can be connected by visiting the Services page
* mtion studio version 0.39.1 or higher is required for this integration
* The mtion studio action allows you to run API Triggers within mtion studio and supply values for parameters to include when run

## v1.1.0.16

* Adding caching of data for VTube Studio and Twitch Integrated Throwing System to reduce the amount of requests made and improve performance & reliability. Cached data will refresh every 30 minutes or can be manually refreshed by hitting the Refresh button from within the VTube Studio or Twitch Integrated Throwing System actions.
* Migrating Tiltify to new v5 API support
* Consumable Actions will now round DOWN to the nearest whole number for the amount used (EX: 0.9999 => 0, while 1.000001 => 1)

## v1.1.0.15

* Adding Pulsoid service connectivity

* Pulsoid can be connected by visiting the Services page.
* A special 20% discount is available for new Pulsoid BRO plan user that also helps support Mix It Up development: <https://pulsoid.net/s/WAoxz>
* The Pulsoid Heart Rate Changed Event command allows you to perform actions as your heart rate changes based on your Pulsoid service settings.
* Adding $pulsoidheartrate Global Special Identifier

* Fixing bug related to certain Crowd Control packs and effects not loading properly within the Crowd Control command editor
* Adding support for all Giveaway Special Identifiers to be usable globally across all commands
* Services are now listed in alphabetical order on the Services page
* Various quality of life & bug fixes

## v1.1.0.14

* Adding YouTube section to Channel page
* Fixing Unmod User and Unban User functionality for Moderation Action for YouTube
* Adding count() function to Special Identifier action
* Adding additional error handling to VTube Studio and Twitch Integrated Throwing System service connectivity
* Various quality of life & bug fixes

## v1.1.0.13

* Adding Twitch Channel Ad Upcoming, Twitch Channel Ad Started, and Twitch Channel Ad Ended event commnads.
* Adding Snooze Next Ad option to Twitch Action.
* Adding $twitchadsnoozecount, $twitchadnextduration, $twitchadnextminutes, and $twitchadnexttime Global Special Identifiers.
* Various quality of life & bug fixes

## v1.1.0.12

* Fixing bug with Tiltify donations not being properly processed

## v1.1.0.11

* Adding ability to specify the platform and user to test a command against
* Adding "Ignore Usage Requirements" option to Command Action when running another command
* Various quality of life & bug fixes

## v1.1.0.10

* Various quality of life & bug fixes

## v1.1.0.9

* Improvements to memory consumption issues and crashes related to out of memory exceptions. Thank you everyone who submitted logs and information to help us further track this issue down.
* Adding logic to handle detecting and merging duplicated user data
* Adding $quotetotal Special Identifier
* Various quality of life & bug fixes

## v1.1.0.7-8

### **IMPORTANT NOTE:** Due to performance and memory issues we have been seeing related to animated emotes within the Chat page, we have temporarily disabled all animated emotes and they will instead be replaced with their static counter-parts. This change only affects animated emotes that are rendered on the Chat page; this does not affect anything with animated images or videos through the Overlay. We have been hard at work attempting to improve the performance of emote rendering in the app to ensure it remains responsive and we have made large improvements to the rendering of static emote images. However the libraries available to use for rendering animated emotes are not to the level of performance and memory efficiency to ensure a quality experience for our users. We will be continuing to investigate alternatives to bring back animated emotes and will keep users aware of any progress we are able to make

* Fixing bug with Twitch Hype Chat event command triggering for regular chat messages
* Performance and memory improvements for rendering emotes on the Chat page
* Adding $streamdescription Special Identifier for use with YouTube streams
* Various quality of life & bug fixes

## v1.1.0.6

* Adding new Special Identifiers for Tiltify service campaign data
* Various quality of life & bug fixes

## v1.1.0.5

* Adding information on Mix It Up Online Alpha pre-registration
* Adding Twitch Channel Updated event command
* Adding Chat User First Message event command
* Adding "Count Lines In File" option to File Action
* Fixing crashing bug when using !uptime command and $streamuptime Special Identifiers

## v1.1.0.4

* Fixing crashing bug when using !uptime command and $streamuptime Special Identifiers

## v1.1.0.3

* Various quality of life & bug fixes

## v1.1.0.2

* Adding the Random Action to allow for the random selection of one or more actions
* Various quality of life & bug fixes

## v1.1.0.1

* Various quality of life & bug fixes

## v1.1.0.0

* Adding support for YouTube Live streaming connectivity
* The following functionality has been added for YouTube

* Event Commands: YouTube Channel Stream Start, YouTube Channel Stream Stop, YouTube Channel New Member, YouTube Channel Member Milestone, YouTube Channel Membership Gifted, YouTube Channel Mass Membership Gifted, YouTube Channel Super Chat
* YouTube Action: Updating Title & Description, Running Ad Break
* Integration with general features and commands where applicable (EX: !uptime, !title, etc)
* BetterTTV emote support for YouTube if you have connected your YouTube account for BetterTTV and enable the feature within Mix It Up under Settings -> Chat
* YouTube supports the ability for users to have spaces in their username, which adjusts how User Special Identifiers are processed. When using any User-based Special Identifier, if the user in question has spaces in their username, then you must use an '@' before their name and the user must be presently active in your chat for us to be able to correlate them.

* Due to the naming conventions used for YouTube, there may be some confusion when using various features in Mix It Up. We are still working on the best way to help distinquish between these conflicting terms, but in the mean-time, a general rule of thumb is as follows:

* On most streaming platforms, a user that is interested notifications for your channel is called a Follower, while a person that is paying money to your stream for special perks is a Subscriber.
* On YouTube, a user that is interested in notifications for your channel is a Subscriber, while a person that is paying money to your stream for special perks is a Member.
* You'll notice that there is an overlap in terminology for Subscriber, but they don't mean the same thing. EX: Twitch Follower = YouTube Subscriber, Twitch Subscriber = YouTube Member
* Within Mix It Up, we have focused on keeping the terminology aligned with what users have been used to on most streaming platforms: Notifications => Follower, Paid User => Subscriber
* If you see anywhere in the app where it specifically says **YouTube** Subscriber, you can safely assume it means a user who is getting notifications for your channel.
* However, if it **does not** have the word YouTube in front of Subscriber, then it means a person who has paid money for perks in your channel

* Adding Twitch Integrated Throwing System service connectivity

* Twitch Integrated Throwing System can be connected by visiting the Services page.
* The Twitch Integrated Throwing action can be added to commands to allow you to throw specific items or activate triggers created in the app.
* The Twitch Integrated Throwing System service connectivity can be used with or without VTube Studio service connectivity, as the two systems are independent of each other.

* Adding Crowd Control service connectivity

* Crowd Control can be connected by visiting the Services page
* A new Crowd Control menu has been added to make dedicated commands that will trigger when a specific game effect has been redeemed
* Crowd Control Effect Redeemed event command has been added for when any effect is redeemed

* Adding Lumia Stream service connectivity

* Lumia Stream can be connected by visiting the Services page.
* The Lumia Stream action can be added to commands to allow you to set lighting settings for all your lights and trigger commands within Lumia Stream

* Adding DonorDrive service connectivity, replacing the previous Extra Life service connectivity

* DonorDrive can be connected by visiting the Services page
* DonorDrive Donation, DonorDrive Donation Incentive, & DonorDrive Donation Milestone event commands have been added

* Adding SAMMI service connectivity

* SAMMI can be connected by visiting the Services page
* The SAMMI action can be added to commands to allow you to trigger buttons, release buttons, and set global variables within SAMMI

* Adding Music Player feature

* The Music Player feature can be found on the main Mix It Up menu.
* Music can be loaded by navigating to the Music Player page and clicking on the Folder icon on the right-side.
* The following are the list of support music file formats: MP3, WAV, FLAC, MP4, M4A, AAC
* The Music Player action can be added to commands to allow you to control the currently playing song from the Music Player and other options.
* Recommended Music Sites: No Copyright Sounds, StreamBeats, Epidemic Sounds

* Adding Infinite Album service connectivity

* Infinite Album can be connected by visiting the Services page
* The Infinite Album action can be added to commands to allow you to adjust the audio style, the instruments, and play various sound effects available from the Infinite Album application

* Adding Alejo Pronouns display support for Twitch chat. This can be enabled under Settings -> Chat
* Adding Twitch Channel Hype Chat event command: $hypechatlevel, $hypechatamount, $hypechatamountnumber, $hypechatamountnumberdigits
* Adding Twitch Channel Outgoing Raid Completed event command for when you the user have completed a raid to another channel.
* Adding Set Content Classification Labels option to Twitch Action
* Adding "Set Command Cooldown" & "Exit Current Command" option to Command Action
* Adding support to "!quote" pre-made chat command for searching for quote based on text input
* Adding settings option to clear all user data for users that have not been seen in X many days
* Adding settings option to specify what text should be used to separated delimited arguments
* Adding settings option for cooldown amount for notifications to reduce back-to-back notification sounds playing
* Updating Trovo connectivity to handle automatic re-authentication when valid
* Fixing bug with the display of FrankerFaceZ emotes
* Various quality of life & bug fixes

## v1.0.0.39

* Updating Twitch event service version for follow events
* Various quality of life & bug fixes

## v1.0.0.37-38

* Various quality of life & bug fixes

## v1.0.0.36

* Removing Twitter integration due to restrictions imposed by Twitter APIs. Information about the removal and alternative solutions can be found on our wiki: <https://wiki.mixitupapp.com/en/services/twitter>
* Various quality of life & bug fixes

## v1.0.0.35

* Fixing issue with Discord service authentication not working due to Discord API change
* Various quality of life & bug fixes

## v1.0.0.34

* Fixing bug with certain pieces of localization text not being shown properly in UI

## v1.0.0.32-33

* Adding support for Twitch's new custom stream tags. Tags are now free-form text of any kind that are 25 characters or less and contain only letters and numbers.
* Adding "Remove Specific Text From File" option to File Action
* Adding Fast Clip option to Trovo Action
* Language localization text updates
* Various quality of life & bug fixes

## v1.0.0.31

* Various quality of life & bug fixes

## v1.0.0.30

* Various quality of life & bug fixes

## v1.0.0.29

* Various quality of life & bug fixes

## v1.0.0.28

* Adding validation check for name for Twitch Bits commands and fixing bug with crash on launch if a command is missing a name

## v1.0.0.27

* Adding support for LoupeDeck service integration
* Adding dedicated Twitch Bits commands
* Updating Twitch Event Sub service URL to new version

## v1.0.0.26

* Fixed critical issue with Twitch Event Sub service
* Various quality of life & bug fixes

## v1.0.0.25

* Various quality of life & bug fixes

## v1.0.0.24

* Adding Send Shoutout option to Twitch Action
* Various quality of life & bug fixes

## v1.0.0.23

* Fixing bug related to Currency/Rank automatic resetting not properly using last reset date
* Various quality of life & bug fixes

## v1.0.0.22

* Fixing bug with Twitch whispers not being sent out correctly
* Various quality of life & bug fixes

## v1.0.0.21

* Various quality of life & bug fixes

## v1.0.0.20

* Various quality of life & bug fixes

## v1.0.0.19

* Fixing hang bug related to daily currency reset setting

## v1.0.0.16-18

* Changing Twitch event connectivity to use new EventSub service for more real-time events
* Adding Twitch Channel Hype Train Level Up event command
* Adding Twitch Channel Charity Donation event command
* Adding Chinese (Traditional) language
* Adding Subscriber Mode On/Off option to Trovo Action
* Adding Image Source and Media Source options to Streaming Software action
* Adding Mute On/Off option to Voicemod Action
* Adding replace() function to Special Identifier action
* Various quality of life & bug fixes

## v1.0.0.15

* Various quality of life & bug fixes

## v1.0.0.14

* Various quality of life & bug fixes

## v1.0.0.12-13

* Fixing crash / hang issue with OBS Studio connectivity for scenes containing lots of sources & adding performance improvements for issues requests
* Various quality of life & bug fixes

## v1.0.0.11

* Fixing issue with the Source Visibility option on the Streaming Software action not working properly for sources in groups in OBS Studio 28
* Various quality of life & bug fixes

## v1.0.0.10

* Adding support for new websocket connectivity in OBS Studio 28. Users will need to disconnect their current connectivity on the Services page and re-connect it after updating OBS Studio and ensuring the websocket settings in OBS Studio are properly configured.
* Various quality of life & bug fixes

## v1.0.0.8 - v1.0.0.9

* Fixing bug with scenario where a user re-name on Twitch can cause duplicated user data
* Adjusting logic for processing Twitch mass mystery gifted subs
* Various quality of life & bug fixes

## v1.0.0.7

* Fixing bug with Game Queue actions not properly handling user changes
* Localization text updates for all languages
* Adding Trovo Channel Magic Chat event command
* Adding Glimesh Channel Subscription Gifted and Glimesh Channel Donation event commands
* Adding filtering of Specialty Excluded users from random user Special Identifiers
* Various quality of life & bug fixes

## v1.0.0.6

* Restoring Channel Page functionality for Twitch and Trovo
* Adding support for Glimesh Channel Subscribed event command
* Adding Streamer, Admin, Moderator, and Subscriber user role tracking for Glimesh
* Commands now show grouped together based on command groups in Command Action
* Game Queue sub-commands on the Game Queue page now have access to the arguments and Special Identifiers used when the user joined the queue
* Adding option to Arguments Usage Requirement to have arguments be assigned to dedicated Special Identifiers
* Various quality of life & bug fixes

## v1.0.0.5

* Adjusting image size used for emotes to counteract and reduce memory & crash problems due to animated emotes. If you still experience issues with memory / crash problems due several animated emotes being sent to your chat, you can optionally disable animated emote rendering by heading to Settings -> Chat.
* Adding the ability to trigger context-menu commands off of the User list on the Chat page
* Fixing bug with $<CURRENCY>alltotal and $<CURRENCY>alltotaldisplay Special Identifiers not processing correctly
* Adding optional parameter to !command pre-made command to specify the name of the group to filter commands down to
* Various quality of life & bug fixes

## v1.0.0.4

* Adding $<CURRENCY>alltotal and $<CURRENCY>alltotaldisplay Special Identifiers
* Adding optional parameter to !command pre-made command to specify the name of the group to filter commands down to
* Various quality of life & bug fixes

## v1.0.0.3

* Fixing bug with backup generation failing due to an issue in the latest database library being used. Library has been rolled back to the last known good and backup generation has been verified to be working again. Any backups generated since version 1.0.0.0 should be re-made to ensure they properly contain all your settings data.
* Various quality of life & bug fixes

## v1.0.0.2

* Fixing bug with !commands pre-made command not showing command list properly
* Fixing bug with new generic event commands not triggering if you don't have a platform-specific event command made
* Various quality of life & bug fixes

## v1.0.0.0

* Trovo:

* Adding support for Trovo streaming platform
* Adding Trovo Spells page to allow custom commands to trigger for specific spells
* Adding Trovo action for adding/removing roles, setting channel mods, and hosting, and other features
* Adding Trovo custom role option to User Role Usage Requirement
* Adding $usertrovocustomroles Special Identifier

* Glimesh:

* Adding support for Glimesh streaming platform

* Twitch

* Adding Set Title, Set Game, and Set Custom Tags options to Twitch action
* Adding additional options to Twitch action for various chat moderation features
* Adding $twitchsubcount and $twitchsubpoints Special Identifier
* Adding $streamtwitchtags Special Identifier
* User subscription dates are no longer available due to breaking changes with Twitch's API. Dates will be tracked with a best-effort basis based on historical tracking in Mix It Up and when subscription events occur while Mix It Up is running.

* Users:

* Adding "Add User" option to Users page to manually add individual user data to Mix It Up directly from a streaming platform
* Basic account linking is now supported for user data between different platforms by using "!linkaccount" in chat
* User data importing now supports handling data from different platforms. Imported data is added to users that Mix It Up currently knows about or is saved for later importing when that user is detected by Mix It Up for the first time.

* Special Identifiers:

* Adding $useraccountdays, $userfollowdays, and $usersubdays Special Identifiers
* Adding $streamislive and $userstreamislive Special Identifier

* Removed Features:

* The Channel page has been removed due to low-usage, breaking changes in Twitch API, and varying functionality differences between different streaming platforms. This page will be brought back at a later date once it has been determined what features are worth bringing forward.
* Offline user viewing time and offline currency/rank acquire rates have been removed

* Adding new Generic event commands that are triggered regardless of what platform you are streaming on so long as the related event command is supported on that platform (IE, it appears under that platforms list of event commands)
* Adding new Wiki site and updating all links within the app to point to new site
* Adding the ability to pause and unpause all queued commands via the Command History page and the Command Action
* Large-scale improvements to automated input via the Input Action and for Hot Key detection support
* Adding support for importing quotes from text or spreadsheet file
* Adding PolyPop service connectivity and PolyPop action
* Adding Enable Chat and Disable Chat options to Moderation Action
* Fixing bug with Developer APIs returning 404 for use and resulting services such as Stream Deck support not working properly
* Various quality of life & bug fixes

## v0.5.11.12

* Fixing bug with selected Custom Overlay Endpoints being reset to the Default Overlay Endpoint when launching the app.

## v0.5.11.10-11

* Replacing legacy API for Twitch with new API support due to upcoming removal at the end of February. As a result of this change, certain pieces of small functionality will no longer be supported until Twitch adds support for them in their new API. The biggest removal of support is the tracking of Subscriber Dates, which will factor in to things like $usersubage and $usersubmonths: <https://blog.twitch.tv/en/2021/07/15/legacy-twitch-api-v5-shutdown-details-and-timeline/>
* Various quality of life & bug fixes

## v0.5.11.9

* Adding custom Webhook Commands feature: <https://github.com/SaviorXTanren/mixer-mixitup/wiki/Webhooks>
* Adding Ukrainian language localization support
* Various quality of life & bug fixes

## v0.5.11.8

* Adding Voicemod service connectivity. This can be enabled by visiting the Services page of the app and using the Voicemod action within commands.
* Adding editor window when testing commands with unique Special Identifiers to allow the editing of the values that are used.
* Various quality of life & bug fixes

## v0.5.11.7

* Adding VTube Studio service connectivity. This can be enabled by visiting the Services page of the app and using the VTube Studio action within commands.
* Adding "Regex Match" option to Conditional Action for validating Regular Expression matching against text.
* Adding support for math expression processing in Counter Action.
* Various quality of life & bug fixes

## v0.5.11.6

* Adding fall-back logic for displaying older Pixel Chat scene components in Pixel Chat action
* Adding uriescape() function to Special Identifier action
* Various quality of life & bug fixes

## v0.5.11.5

* Adding Pixel Chat service connectivity and Pixel Chat action
* Various quality of life & bug fixes

## v0.5.11.4

* Adding $bitslifetimeamount Special Identifier for Twitch Channel Bits Cheered event command
* Having Specialty Excluded option for Users also exclude from random-based Special Identifiers
* Fixing bug with Arguments Usage Requirement not saving properly
* Fixing bug with individual user data loading failing in some circumstances
* Various quality of life & bug fixes

## v0.5.11.3

* Screenshots can now be optionally uploaded with Community Commands to help showcase what the command does
* Re-enabling User Data Import functionality on the Users page
* Fixing bug with chat message deletion detection failing sometimes when deleted from outside of Mix It Up
* Various quality of life & bug fixes

## v0.5.11.2

* Adding Alerts option for Hype Train events
* Fixing bug with back-up logic for donation user processing not properly creating a fake user with the supplied donation username
* Fixing bug with whispers sent from streamer or bot account triggering the Chat Message Received event command
* Various quality of life & bug fixes

## v0.5.11.1

* Allowing for rating-only reviews for Community Commands
* Various quality of life & bug fixes

## v0.5.11.0

* Community Commands:

* Re-adding the Community Commands feature (formerly known as the Mix It Up Store), which allows users to upload commands they have created and search through commands other users have made.
* By visiting the Community Commands section from the main menu, you will be able to see some of the highlighted commands available, search for commands, and see the commands you have uploaded
* Users can rate & review commands based on how well they worked and how much they liked them. They can also report commands if they deemed malicious or inappropriate.
* Commands can be uploaded by editing an existing command you have and selected "Upload Command" in the bottom-right of the command editor window. A command must be saved first before the option will be available.
* Uploaded commands can have their metadata (name, description, & image) edited by visiting the command inside of the Community Commands feature and selecting Edit or remove a command by selected Delete.+ Commands can be updated by simply re-uploading the same command again. Commands are tied based on their internal ID, so if you delete your local copy of your command, you will be unable to upload it to the same community command.

* Events

* Twitch:

* We have financed a server to allow us to handle Twitch events that require Twitch's Web Hooks system. This will allow us to remove the 1 Minute Delay on certain events, as well as add support for brand-new events that were not previously possible.
* The following events have been updated to be real-time: Twitch Channel Stream Start, Twitch Channel Followed
* The following events have been added: Twitch Channel Stream Stop, Twitch Channel Hype Train Begin, Twitch Channel Hype Train End

* Twitch Channel Hype Train Begin includes the following Special Identifiers: $hypetraintotalpoints, $hypetrainlevelpoints, $hypetrainlevelgoal
* Twitch Channel Hype Train End includes the following Special Identifiers: $hypetraintotallevel, $hypetraintotalpoints

* In the event of an outage of our server or a local disconnection, the Twitch Channel Stream Start & Twitch Channel Followed events will fall back to their old 1 Minute Delay logic, while the newly added events will be unavailable.
* Adding $messagenocheermotes Special Identifier to Twitch Channel Bits Cheered event command that has only the text of the user's message and no cheermotes in the message
* Message text is now available as Argument Special Identifiers for the Twitch Channel Resubscribed & Twitch Channel Bits Cheered event commands

* Renaming StreamJar service to Rainmaker service, updating authentication for service, and adding support for real-time donation events
* Fixing breaking change with StreamElements service where data being sent down was in a new format that it had previously been which was causing donations to not be properly detected

* Usage Requirements:

* Adding advanced, multi-role selecton for User Role usage requirement
* Adding Arguments usage requirement which allows you to set required or optional arguments to a command based on it's type (Number, Decimal, Text, User). If a required argument is missing, an error message will be shown with the name of the arguments and correct ordering.

* Actions:

* Twitch Action:

* Adding Poll options to Twitch action. Sub-actions can be added that will be run when the poll is complete and will contain the $pollchoice Special Identifier for the winning poll choice. This action **DOES NOT** wait for the poll to complete
* Adding Prediction options to Twitch action. Sub-actions can be added that will be run when the prediction is complete and will contain the $predictionoutcome Special Identifier for the winning prediction. This action **DOES NOT** wait for the prediction to complete
* Adding Special Identifier processing for various fields on the Update Channel Point option

* Adding "Repeat While True" option to Conditional action to allow repeated looping for sub-actions while the conditional clauses remain true
* Adding option Currency/Rank/Inventory action for whether users must be present in chat
* Adding urlencode() function to Special Identifier action

* Special Identifiers:

* Adding $message Special Identifier for all chat-based commands that contains the entire chat message used
* Adding $streamlootscarddescription and $streamlootscardalertmessage Special Identifiers for Streamloots event commands & Streamloots Card commands
* Adding $argdelimitedXtext and $argdelimitedcount Special Identifiers that split argument text based on the '|' character with leading & trailing spaces automatically removed. For example, the text "Hello World | How are you| on this fine day" would have 3 arguments of $argdelimited1text, $argdelimited2text, $argdelimited3text containing the respective text of "Hello World", "How are you", "on this fine day".
* Adding $usersubbadge Special Identifier
* Adding $userfulldisplayname Special Identifier
* Adding $userismod Special Identifier for whether the current user has the Moderator role
* Adding $userdisplayroles Special Identifier for the language localized version of roles for a user
* Adding $userinchat Special Identifier. Accuracy for this Special Identifier can't be guaranteed due to the unreliability of chat notifications sent down to us. All that can be guaranteed is that the user is either currently in chat or was in chat at some point during the specific instance of Mix It Up

* Removing the ability to follow/unfollow a user from the User pop-up dialog in Chat due to Twitch deprecation of the corresponding API: <https://blog.twitch.tv/en/2021/06/28/deprecation-of-create-and-delete-follows-api-endpoints/>
* Adding filter options to Users page
* Adding Notes section to User editor for adding details information about individual users
* Adding additional error messaging and updating display text for Channel Point Reward buttons & functionality
* Adding memory & performance usage improvements by only loading in user data when it is needed rather than loading all user data in on launch
* Adding enforcement for targeted users to be detected in chat for Game commands
* Adjusting display name for user's to better match how it is displayed natively in Twitch for alternative languages
* Adding UI support for bit & follower channel emotes
* The Chat Message Overlay Widget now processes the {USER\_SUB\_IMAGE} text for the URL of the user's subscriber badge image
* Fixing bug with Duel game command not properly refunding user if the target user does not accept
* Various quality of life & bug fixes

## v0.5.10.4

* Adding additional timeouts to Streamlabs & StreamElements service connections to combat rapid disconnection issues to their servers

## v0.5.10.3

* Various quality of life & bug fixes

## v0.5.10.2

* Adding extra connection protection & retry logic for unstable Streamlabs and StreamElements service connections
* Adding $randomregularuser\_\_\_\_ Special Identifier
* Added Input Action support for F13 to F24
* Various quality of life & bug fixes

## v0.5.10.1

* Adding Export Actions option to Conditional Action
* Fixing bug with Command Action not performing Usage Requirements & Validation of the selected sub-command when using the Run Command option
* Various quality of life & bug fixes

## v0.5.10.0

* Command System Improvements:

* Adding new Command History page that shows the most recent triggered commands, what their progress is, and the ability to cancel & retry them
* Adding Command Lock System option to select what types of commands are locked under Settings -> Commands
* Adding Requirement Errors Cooldown Type option to determine how requirement error messages should be cooled down under Settings -> Commands
* Exported \*.miucommand command files will now show Mix It Up icon in Windows Explorer and can be opened to import data into a new or existing command
* Exported \*.miucommand command files can now be drag & dropped into a command editor window to import the actions from it
* Adding Cancel All Commands option to Command Action

* Twitch Channel Point Reward Commands:

* Twitch Channel Point Rewards can now be created directly from the Channel Points page of the app. This will create a basic reward with the name specified and additional details for the reward must then be configured up on Twitch's website
* Twitch Channel Point Rewards that are **created through Mix It Up** can now have their properties dynamically updated using the Twitch action

* Adding Streamloots Cards page for creating dedicated commands that are triggered when specific Streamloots cards are redeemed. **Only shown when Streamloots service is connected.**
* Twitch Mass Subscriptions Gifted event command now includes all users that received a gifted sub as arguments to the command. **Requires the Twitch Mass Subscriptions Gifted Filter option enabled under Settings -> Commands**
* Chat Message Received event command now includes the full message as arguments to the command
* The StreamElements donation event command can now be triggered in real-time and is no longer delayed by up to 1 minute
* Adding settings menu for Counters to allow for viewing, editing, & deletion
* Adding event command for StreamElements Merch Purchase
* Using the Test button for the End Credits Overlay Widget will now only fill in test data if the given category doesn't already have users set for it and will only clear out data for those categories
* Adding uninstaller and registering in Windows add/remove programs
* Various quality of life & bug fixes

## v0.5.9.7

* Fixing crashing bug related to processing certain FrankerFaceZ emote images
* Allowing text entry & Special Identifier processing for item name in Currency/Rank/Inventory action
* Various quality of life & bug fixes

## v0.5.9.6

* If you are already editing a command and attempt to open a 2nd window of the same command, it will now not open the 2nd window and instead focus the 1st window to prevent users from accidently saving an old or inaccurate version of the same command
* Fixing bug with Coin Pusher & Volcano game types not properly saving out their total amounts between sessions
* Various quality of life & bug fixes

## v0.5.9.5

* Fixing crashing bug related to missing localization text for some languages
* Adding Follower Event Moderation setting that allow for setting a maximum amount that can queue up before they are ignored
* Adding initial 5 second cooldown on error messages for Cooldown Requirement to reduce spam of multiple users attempting to trigger a command at the same time
* When running a command with a Currency Requirement of Minimum Only or Maximum & Minimum, the amount used will default to the Minimum Amount if the user does not specify an amount or we are unable to detect the amount
* Various quality of life & bug fixes

## v0.5.9.4

* Updated localizations for Russian, Portuguese, Japanese, Spanish, German, French, & Dutch
* Various quality of life & bug fixes

## v0.5.9.3

* Adding initial support for Russian language
* Adding the ability to import old command format files temporarily. Please ensure if you are the creator of a command that is using the old format that you export your command in the newer format sometime soon, as this functionality will be removed.
* Adding Special Identifier processing for Discord image file path
* Fixing bug with Prime sub plan not being properly detected for subscription events
* Adding $gameanswerX Special Identifiers to Trivia game command
* Fixing bug with Trivia game command not showing Started sub-command in editor UI
* Adding direct link button on Channel Points page that links directly to editor on Twitch
* Various quality of life & bug fixes

## v0.5.9.2

* Re-adding Save Chat Event Logs settings option to Settings -> Chat UI
* Various quality of life & bug fixes

## v0.5.9.1

* Updating Command Action to enable command locks if Wait Until Complete option is NOT selected
* Fixing bug with certain Game Special Identifiers not processing correctly
* Fixing bug with certain Game values not re-saving properly when editing an existing game
* Fixing bug with Translation action still appearing in list of selectable actions to add to a command

## v0.5.9.0

* Command Updates:

* Updating command editor to include detailed error messages for command settings, requirements, and actions
* Twitch Channel Points command editor now has drop-down in Name field that lists the names of all the custom Channel Point Rewards the user has created on Twitch
* Command lists such as Chat, Timers, & Action Groups will now show a simplified version of the list instead if there is only 1 Command Group for all of the commands
* Adding support for Twitch Channel Point commands groups

* Usage Requirement Updates

* Each requirement's error messages now have a cooldown to how often they are shown in chat to help minimize on spam. The default value is 10 seconds and can be configured by going to Settings -> Commands
* Currency, Rank, & Inventory requirements now support the ability to have multiple per command
* Adding new VIP Exclusive role to Usage Requirements that will only allow VIPs and not Subs, as opposed to the current VIP role which allows VIPs & Subs. Moderators and higher will still be able to run the command
* Adding Run For Each User option to Threshold requirement to have command trigger for each user that participated
* Adding settings option to include the name of the user that attempted to run the command when sending error messages

* Action Updates:

* Adding the ability to enable/disable individual actions within a command
* Conditional actions have been changed to allow for multiple actions to be run when they are triggered
* Deprecating Translation action due to low usage & breaking change with the translation API service used
* Streaming Platform -> Twitch Action:

* The Streaming Platform action has been changed to the Twitch action
* The VIP User and Un-VIP User options have been moved from the Moderation action to the Twitch action
* The Clips action has been moved into the Twitch action
* Adding the ability to set a Stream Marker via the Twitch action

* Command Action:

* When a command action is used, by default it will now wait for the selected sub-command to finish before continuing on in the parent command
* Existing command actions will NOT have the above setting enabled and will function as they used to, meaning they will NOT wait for the sub-command to finish
* Adding option to Command Action to determine whether sub-command should wait to finish before proceeding on to next action
* Adding buttons to Command Action to auto-create a new Action Group and edit the select command

* Streaming Software Action:

* The Streaming Software action's source-based options can now handle folders and sub-items inside folders for OBS Studio. This may require re-installing the OBS web socket if you are running an older version.
* Adding Source Filter Visibility option to Streaming Software action for OBS Studio

* Web Request Action:

* The Web Request action now stores its result into a Special Identifier called $webrequestresult and its output options have been removed and instead replaced with their respective actions.
* Adding the ability to process Special Identifiers for JSON Keys in Web Request Action
* Updating Web Request action to support local file reading

* File Action:

* Adding "Insert To File At Specific Line" option to File Action
* Allowing for save-based File Actions to have an empty set of text to save

* Currency actions now process Math equations in the Amount field
* External Program actions now have the ability to store their output into a Special Identifier called $externalprogramresult
* Adding length() function to Special Identifier action

* Game Updates:

* All game commands have received internal re-working for logic
* The Pickpocket game type functionality has been merged into the Steal game type
* The Beach Ball game type functionality has been merged into the Hot Potato game type
* The Vending Machine game type functionality has been merged into the Spin game type

* Special Identifier Updates:

* Adding $streamcurrentscene Special Identifier for name of the current scene in OBS Studio & Streamlabs OBS
* Adding $gamequeuetotal for the total number of users in the Game Queue
* Adding $webrequestresult Special Identifier for the result of a Web Request action
* Adding $externalprogramresult Special Identifier for the result of a External Program action when option is toggled
* Adding $userisvip Special Identifier
* Adding $gamewinnerscount Special Identifier
* Adding $commandname Special Identifier for the current running command
* Adding $userdisplayname Special Identifier

* Removing Clip Playback Overlay Widget due to changes with Twitch's clip playback system
* Adding Alert settings option for Donations
* Adding Rank Down Command to Rank Systems
* Fixing bug with BetterTTV emotes not being properly loaded when app launched
* Various quality of life & bug fixes

## v0.5.8.5

* Fixing bug with Permission value for Stream Pass not being respected properly when adding points to a user
* Adjusting Tiltify service connectivity to use regular web browser window for authentication to work around changes to Tiltify page
* Fixing bug with Notification volume settings not saving properly
* Fixing bug with raids contributing to gifted subs statistics trackers
* Various quality of life & bug fixes

## v0.5.8.4

* Adding loop option to Overlay Video action and Overlay Video widget
* Adding urlencode() processor function to Special Identifier action
* Fixing bug with Tiltify team campaigns not properly being queried for donations when configured
* Command filtering search now searches through group names and includes all commands that are part of said group
* Various quality of life & bug fixes

## v0.5.8.2

* Adjusting gifted sub logic to make sure other gifted sub-related work is run (Latest Special Identifiers, currency, etc), but only the event command & alert if it's not bundled under a mass gifted sub event
* Adjusting follower detection logic to buffer scenario where we fail to get the most recent followers on initial load
* Various quality of life & bug fixes

## v0.5.8.1

* Adding $usercolor and $usertwitchcolor Special Identifiers
* Moderation error message for chat participation now state the user's name who's message was deleted
* Stream Pass level up commands now translate the Special Identifier for the user's level to match the level that the command occurred for
* Streamer & Bot accounts will now not show up for Leaderboard Overlay Widgets
* Various quality of life & bug fixes

## v0.5.8.0

* Large-scale re-working of Settings menu, adding new areas and moving various options to better locations
* Adding filtering option to Commands settings for Twitch Subscription Gifted & Mass Subscriptions Gifted event commands to provide a better experience:

* This option allows you to set a filter amount for the Mass Subscriptions Gifted to determine which command is run
* If the amount gifted is LESS THAN OR EQUAL to the filter amount, then it will only run the Subscription Gifted command once for each user who was gifted a sub
* If the amount gifted is GREATER THAN the filter amount, it will only run the Mass Subscriptions Gifted command once in total
* This option is enabled by default and set to 1, it can be disabled or changed by going to Settings -> Commands

* Adding Commands menu to Settings for command & action-based options:

* Filtering option for Twitch Mass Subscriptions Gifted event command based on amount of sub gifted
* Adding the ability to hide specific action types from the actions list

* Adding Alerts menu to Settings for chat alert-based options:

* Enable/disable individual alert options in more detail
* Set colors for each individual alert type

* Adding the following options to the Chat menu in Settings:

* Username colors now default to the user's set value on Twitch
* Custom username colors can be set based on the role of the user
* Hiding a user's avatar, role badge, subscriber badge, and specialty badge
* Hiding messages sent by Bot account
* Add line seperators between messages
* Alternating background colors for messages

* Adding the following options to the Notifications menu in Settings:

* Dedicated audio device to use just for notifications

* Adding the following options to the Users menu in Settings:

* Option to clear all Mixer user data permanently from your settings
* Moving option to clear all user data from Advanced section to Users section

* Automated settings backups will now default to the Mix It Up settings folder when enabled unless a location is set
* Users that are gifted a sub and choose to continue it by paying for the next month's sub will now trigger the Subscribed event command
* Adjusting Twitch Channel Points Redeemed event command & Channel Points commands to only translate $message if there is a valid message sent by the user and having it also passed in as arguments to the command
* Messages that are highlighted by the Twitch Channel Points Reward now show as highlighted in the Mix It Up chat UI
* Discord actions now list announcement channels for community servers in the list of selectable channels
* Adding Manual Reset option on Inventory editor window
* Fixing bug with Bit Cheermotes where individual, non-standard amounts did not display properly (EX: A single bit cheermote for 3 bits)
* Fixing bug with month computation where it would not include the interim month (EX: Someone has been subbed to you for 40 days, $usersubmonths should be 2)
* Adjusting logic for handling timezone adjustment for Twitch dates & times
* Various quality of life & bug fixes

## v0.5.7.3

* Force reseting Preview Program option for all users, please manually re-enable this option if you wish to stay in the Preview Program
* Officially deprecating Moderator login and adding reference to Twitch Moderator View
* Fixing bug with Latest Special Identifiers not loading correctly when app is re-launched due to serialization
* Have follow/unfollow button update to show current state on Users pop-up dialog
* Removing auto-assignment of Mod role for Channel Editors
* Updating available text to speech voices
* Various quality of life & bug fixes

## v0.5.7.2

* Adding $usersubtier Special Identifier
* Various quality of life & bug fixes

## v0.5.7.1

* Updating various event command names to include reference to 1 minute delay
* Allowing Reset On Load option for Counter actions to be toggable without having the Save To File option toggled
* Fixing bug with Streaming Platform action not properly saving Raid option
* Various quality of life & bug fixes

## v0.5.7.0

* Adding support for Twitch & the removal of support for Mixer
* Large-scale updates to our Wiki to reflect all changes made as part of the migration: <https://github.com/SaviorXTanren/mixer-mixitup/wiki>
* The following features have been removed and will not be returning:

* MixPlay
* Mixer-specific event commands
* Sparks, Embers, & Milestones
* Auto Hoster

* The following features are being temporarily removed and will be returning in some form after changes and/or investigation:

* Mix It Up Store
* Mix It Up Remote
* New User Wizard importing

* The following features have been replaced by an equivalent feature:

* All Mixer event commands have been migrated over to their Twitch equivilents if they were possible. Please see our Wiki page for a full explanation of how each new event command works: <https://github.com/SaviorXTanren/mixer-mixitup/wiki/Events>
* MixPlay has been replaced by its equivilent of Channel Points. Custom Channel Point Rewards that are created can be linked to a command in Mix It Up that will automatically be run when the reward is purchased: <https://github.com/SaviorXTanren/mixer-mixitup/wiki/Channel-Points>
* The Mixer Clips action has been removed and the Clips action has been added. You will need to manually add this action to any existing commands that used to use the Mixer Clips action
* The Stream Clip Playback Overlay Widget has been removed and the Clip Playback Overlay Widget has been added. You will need to manually add this Overlay Widget to use this functionality
* Features that took advantage of Sparks & Embers have now been updated to use Bits instead
* Chat User Timeout: Does not indicate who performed the timeout, $targetuser\_\_\_ is the person that received the timeout, $timeoutlength indicates the length of the timeout in seconds
* Chat User Ban: Does not indicate who performed the ban, $targetuser\_\_\_ is the person who was banned

* Various Special Identifiers have been adjusted or removed based on what data is available on Twitch. The following is a summary of the changes made, please see our Wiki page for full details: <https://github.com/SaviorXTanren/mixer-mixitup/wiki/Special-Identifiers>

* $userage has been replaced by $useraccountage
* Adding $usertotalbitscheered
* Removing $userchannelid, $userchannellive, $userchannelfeatured, & $usergameimage
* Adding $userstreamtitle, $userstreamgame, $userstreamfollowercount, & $userstreamviewscount
* Adding $topXbitscheered\_\_\_\_, $topbitscheereduser\_\_\_, & $topbitscheeredamount
* All $uptime\_\_\_ & $start\_\_\_ Special Identifiers have been replaced by $streamuptime\_\_\_ & $streamstart\_\_\_
* $streamagerating & $streamhostcount have been removed
* $streamfollowcount has been replaced by $streamfollowercount
* $streamsubcount has been replaced by $streamsubscribercount
* $streamviewertotal has been replaced by $streamviewscount
* $featuredchannel & $costreamusers have been removed
* $randomsubuser\_\_\_ has been replaced by $randomsubscriberuser\_\_\_

* Special Identifiers that are unique to a specific feature have been moved to that respective feature's Wiki page. For example, to see what Special Identifiers only work with Channel Points, visit the Channel Points Wiki page
* Due to limits & restrictions that Twitch places on whispers, all automated chat whispers from things such as error messages that relied on whispers will now appear as regular chat messages. Anything that is specifically indicated to be a whisper by you, such as via a Chat action, will continue to be an actual whisper
* Adding !linkmixeraccount & !approvemixeraccount pre-made chat command to assist with merging the user's Mix It Up data from their Mixer account over to their Twitch account
* The following services will be automatically disconnected and must be manually reconnected to change from your Mixer account to your Twitch account: Streamlabs, StreamElements, TipeeeStream, TreatStream, StreamJar
* Adding the ability to specify the required tier for Subscriber role for Role Usage Requirements
* Adding support for BetterTTV & FrankerFaceZ emotes. This can be enabled by heading to Settings -> Chat
* Patreon patrons that have their Twitch account link will now improve user account pairing process with Mix It Up
* When restoring backed up settings, the restoration process will not occur after the application restarts to prevent an occassional locking state that can occur
* Updating Developer APIs to support querying information for Twitch users
* Adding the following new themes: Mixer, Twitch, Atl3m's Plexify, BlueLeprechaunTV
* Various quality of life & bug fixes

## v0.5.6.0

* Adding new Stream Pass feature
* Adding new Redemption Store feature
* Adding initial rollout of Usage Requirement v2 to Redemption Store:

* Multiple currency, rank, & inventory requirements can now be applied to a single command
* Threshold requirements now allow for the command to be run for every user that participated
* Patreon Benefit has been moved from Settings to User Role requirement

* Adding $toptimeuser\_\_\_, $topsparksuseduser\_\_\_, $topembersuseduser\_\_, $top<CURRENCY>user, & $top<RANK>user Special Identifiers to get the user with the highest amount
* Adding Regular Bonus field to Currency/Ranks
* Currency/Rank Role Bonuses now apply only to a user if they specifically have that role (Regular, Subscriber, Mod). If multiple Role Bonuses apply to a user, only the highest one is used
* Inventory items can now be renamed without losing user counts for them
* Adding new fields to User data exporting functionality
* Improvements to User data importing functionality
* Adding Export Quotes option to Quotes page
* Various quality of life & bug fixes

## v0.5.5.8

* Adding "Not Replaced" operator to Conditional action
* Adding Quotes endpoints to Developer API
* Fixing bug related to chat command trigger caching not updating correctly in certain scenarios
* Various quality of life & bug fixes

## v0.5.5.7

* Adding initial language localization support for Dutch, French, Spanish, Japanese, Portuguese, and German
* Adding $usergiveawayentries and $usergiveawaytotalentries Special Identifiers to Giveaway User Joined command
* Fixing bug with user details not refreshing for Special Identifier processing
* Adding Mod User & Unmod User options to Moderation action
* Various quality of life & bug fixes

## v0.5.5.6

* Various quality of life & bug fixes

## v0.5.5.5

* Fixing bug with New User Wizard re-running on start up when activated via Settings -> Advanced
* Adding additional diagnostic logging to help diagnose various issues
* Various quality of life & bug fixes

## v0.5.5.4

* Various quality of life & bug fixes

## v0.5.5.3

* Fixing bug with importing of data during New User Wizard
* Fixing permissions bug with running ads through the Streaming Platform action
* Various quality of life & bug fixes

## v0.5.5.2

* Chat message & command processing performance tweaks
* Updating Streamlabs donation checking to use real-time web socket connection
* Fixing bug with game sub-commands not running in some cases
* Fixing bug with alert chat messages not processing correctly
* Various quality of life & bug fixes

## v0.5.5.1

* Actions can now be dragged & dropped within a command to re-order them
* Adding the ability to run /timeout & /ban commands from Chat window
* Fixing bug where chat alert messages did not contain the correct user for right-click menus
* Fixing bug where signing in to a bot account when app is fully launched did not log the bot account into chat
* Fixing bug where MixPlay controls with emojis weren't being updated upon connection to fix design issue with MixPlay
* Various quality of life & bug fixes

## v0.5.5.0

* **BREAKING CHANGE:** Removing Song Requests feature <https://mixitupapp.com/songrequests>
* **BREAKING CHANGE:** Removing Twitter Retweet event command due to rate limiting issues with Twitter
* **BREAKING CHANGE:** The Chat User Banned event command has been updated to support tracking the user that performed the ban. $user\_\_\_\_ now represents the user who triggered the ban, while $targetuser\_\_\_\_ now represents the user that was banned.
* Adding StreamElements service connectivity and StreamElements donation event command
* Adding initial language localization support for German (de-DE)
* Upgrading settings data formatting to improve reliability and integrity
* Large-scale rework of services connectivity, events, and MixPlay logic to improve speed and reliability
* Adding improved error messaging to connections & services to assist with diagnosing issues for users
* Adding MixPlay spamming input detection for visual cooldowns to prevent extra MixPlay command triggering
* Adding the Mix It Up Overlay as a sound device option for the Sound action
* Adding "Run Ad" option to Streaming Platform action
* Adding "Replaced" comparator to Conditional Action to determine if the 1st value has been changed or replaced due to Special Identifier processing
* Adding Chat User Timeout event command with $timeoutlength Special Identifier
* Adding Chat Whisper Received event command
* Adding $usersubstreak Special Identifier to Mixer Channel Resubscribed event
* Adding $userisregular Special Identifier
* Adding latest Special Identifiers to keep track of last user/usage for various features: $latestfolloweruser\_\_\_\_, $latesthostuser\_\_\_\_, $latesthostviewercount, $latestsubscriberuser\_\_\_\_, $latestsubscribersubmonths, $latestsparkusageuser\_\_\_\_, $latestsparkusageamount, $latestemberusageuser\_\_\_\_, $latestemberusageamount, $latestdonationuser\_\_\_\_, $latestdonationamount
* Adding Special Identifier for Currency/Rank/Inventory to show display-friendly numbers: $user\_\_\_\_display
* Adding Special Identifier for Currency/Rank/Inventory to show position relative to all users: $user\_\_\_\_position
* Adding support for $skill\_\_\_\_\_ Special Identifiers in Channel Embers Spent event command
* Adding $userpatreontier Special Identifier
* Adding $milestoneendtime & $milestonetimeremaining Special Identifiers
* Adding $streamviewertotal & $streamchattercount Special Identifier
* Adding option to pin Dashboard window to top of screen
* Revamping New User Wizard experience to better assist and onboard new users
* Adding Accounts page to main menu and moving Mixer Bot Account authentication to this page
* MixPlay actions with the Cooldown option now support Special Identifiers
* Special Identifiers created by Web Request actions are no longer global Special Identifiers
* Adding $isautohost Special Identifier to Mixer Channel Hosted event to indicate if the host occurred by Mixer's built-in auto-hosting
* Updating list of supported Text to Speech voices
* Improving logic with age calcuations for things like follow age, subscriber age, etc
* Fixing bug with WASD buttons being reversed for MixPlay Joystick Commands
* Various quality of life & bug fixes

## v0.5.4.8

* Fixing broken Overlay animation by updating animation library URL
* Fixing broken Text to Speech functionality due to service changes
* Various quality of life & bug fixes

## v0.5.4.7

* Various quality of life & bug fixes

## v0.5.4.6

* Increase timeout for the Mixer Constellation service to avoid login failures

## v0.5.4.5

* Various quality of life & bug fixes

## v0.5.4.4

* Various bug fixes for the End Credits Overlay Widget
* Various quality of life & bug fixes

## v0.5.4.3

* Possible fix for some users that are experiencing hours & currency/rank not saving between sessions for users
* Various quality of life & bug fixes

## v0.5.4.2

* Various bug fixes for the End Credits Overlay Widget
* Various quality of life & bug fixes

## v0.5.4.1

* Various quality of life & bug fixes

## v0.5.4.0

* Adding End Credits Overlay Widget
* Adding the ability to select Single Action for Conditional Actions
* Adding Trivia game type
* Adding JustGiving service integration
* Adding the ability to upload a file when sending a message to Discord
* Adding additional user metrics for tracking
* Adding dedicated Play button when editing a command for easy testing
* Adding drop-down to Stream Title on Channel page to include last 5 stream titles
* Adding option to convert basic commands to advanced commands when editing them
* Switching Mixer Alerts over to new endpoint
* Various quality of life & bug fixes

## v0.5.3.2

* Re-publishing to fix issue with GitHub update hosting
* Various quality of life & bug fixes

## v0.5.3.1

* Fixing bug with chat commands being case-sensitive
* Fixing bug with message deletion not showing who deleted the message
* Updating map image for Fortnite drop map
* Various quality of life & bug fixes

## v0.5.3.0

* Large-scale overhaul of the internal Chat logic for better performance
* Adding new Dashboard window with the following items: Chat, Alerts, Quick Commands, Statistics, Game Queue, & Song Requests
* Chat commands now support the use of wild card triggers
* Adding the ability for Chat Commands to be enabled for Chat Context Menu usage
* Adding support for multiple clauses to Conditional action
* Adding rate limiting checks to the Send Message option for the Discord action to cut down on abuse & adding support for custom Discord application usage to circumvent rate limiting
* MixrElixr emotes can now be enabled in the Chat Settings menu to be used and displayed in chat
* Adding trade command option to Inventories
* Adding Chat Message Deleted event command
* Chat timestamps can now be enabled in the Chat Settings menu
* Adding support for team campaigns to be selectable for Tiltify service
* Added "Sparks & Embers Only" and "Embers Only" chat moderation rules
* Various quality of life & bug fixes

## v0.5.2.11

* Fixing bug with Song Requests buttons not working
* Fixing bug with MixPlay timeout not correctly identifying the right participant
* Fixing various bugs to XSplit connectivity
* Various quality of life & bug fixes

## v0.5.2.10

* Adding Streamloots Pack Purchased & Pack Gifted event commands
* Various quality of life & bug fixes

## v0.5.2.9

* Adding support for longMessage field on Streamloots card if message field doesn't exist
* Adding Streaming Action Start/Stop Stream support for XSplit
* Fixing bug with Overlay Timer Widget counting down faster when it's disabled before it's completed
* Various quality of life & bug fixes

## v0.5.2.8

* Adding $userisfollower & $userissubscriber Special Identifiers
* Tweaking Clips creation logic to work around broken Mixer API
* Adding logic to remove unused cooldown & command groups on start up
* Fixing bug with Overlay Follower Goal/Progress Bar not counting correctly
* Adding the ability to specifiy the number ID of a game when running !setgame command

## v0.5.2.7

* Adding $streamlootscardvideo & $streamlootscardhasvideo Special Identifiers
* Updating Sound Action to support web-based sound files
* Adding blocking, error message if the MixPlay project contained controls with the same ID when trying to connect

## v0.5.2.6

* Fixing bug with Streamloots Card Redemption where TTS-enabled cards would fail to trigger correctly

## v0.5.2.5

* Fixing possible bug with Streamloots Card Redemption where full card data isn't acquired correctly
* Adding $streamlootscardsound Special Identifier
* Various quality of life & bug fixes

## v0.5.2.4

* Adding Streamloots integration & Streamloots Card Redeemed event command
* Adding support for Streaming Software action's Save Replay Buffer option for Streamlabs OBS
* Updating TipeeeStream connection logic for new servers
* Various quality of life & bug fixes

## v0.5.2.3

* Adding tolower(), toupper(), and removespaces() functions into Special Identifier action for text-based replacements
* Fixing bug with Channel Milestone Reached event command trigger when it shouldn't
* Various quality of life & bug fixes

## v0.5.2.2

* Updates to support new Spark Patronage changes. All Special Identifiers have remained the same, but anything that showed money amounts now show percentage boosts
* Adding "New Leader" command to Overlay Leaderboard Widget
* Adding option to Song Requests Overlay Widget for whether to include the currently playing song
* Various quality of life & bug fixes

## v0.5.2.1

* Adding list vertical alignment option to list-based Overlay Widgets
* Updating Fortnite drop map to Season 10
* Various bug fixes to Overlay Widgets
* Various quality of life & bug fixes

## v0.5.2.0

* BREAKING CHANGE: Chat User Purged event command now specifies the moderator who performed the purging as $username, while the user that was purged is not $targetusername
* BREAKING CHANGE: All random user Special Identifiers will always give a new user every time they are used instead of saving them for the entire command. If you need to use the same user multiple times, use it inside of a Special Identifier action.
* Large-scale overhaul of Overlay system; more Overlay Widgets handle updating dynamically
* Adding IFTTT action to support firing of triggers for IFTTT events
* Adding Streaming Platform action to support streaming website-specific actions such as hosting & polls
* Adding Ticker Tape & Spark Crystal Overlay Widgets
* Adding Healing & Overkill bonus settings to Stream Boss Overlay Widget
* Adding Fade Out options to Chat Messages & Event List Overlay Widgets
* Adding the ability to specify the layer that an Overlay item is positioned at
* Moving Overlay Widget refresh timer into individual Overlay Widgets to allow for greater flexibility
* Adding Bet, Word Scramble, Treasure Defense, Hot Potato, Beach Ball, & Hangman game types
* Adding "Not Enough Players" & "Not Accepted" command to appropriate games
* Changing setup for Bet game type and removing all past instances of it
* Adding the ability to specify a message to whisper to Streamer when auto-hosted
* Adding Song Removed trigger command for Song Requests
* Adding Chat User Joined & Left event commands
* Adding "!startgiveaway" pre-made chat command
* Adding $usergamequeueposition, $dateyear, $datemonth, $dateday, $timehour, & $timeminute Special Identifiers
* Adding Set option to Counter action
* Adding volume level adjustment for notifications
* Adding automatic rounding up Special Identifiers that are used in Currency/Rank/Inventory actions to prevent the need for an additional Special Identifier action
* Changing File Action to create local instead of global Special Identifiers
* Removing Action Group Action & replacing with Command Action
* Removing GawkBox service
* Various quality of life & bug fixes

## v0.5.1.6-7

* Fixing updating of channel title, game, & description due to Mixer issue
* Various quality of life & bug fixes

## v0.5.1.5

* Fixing bug with some Spotify playlists not being properly handled for Song Requests
* Various quality of life & bug fixes

## v0.5.1.4

* Fixing bug with Emotes & Skills Only moderation not correctly detecting skills
* Fixing bug where giveaway inventory requirement would not subtract the proper amount for multiple entries
* Various quality of life & bug fixes

## v0.5.1.3

* Fixing bug with users not being removed from chat list when they leave
* Various quality of life & bug fixes

## v0.5.1.2

* Various quality of life & bug fixes

## v0.5.1.0

* Adding service integration with OvrStream & OvrStream action
* Adding Fan Progression interval for Currency/Ranks
* Large-scale performance & reliability improvements to Song Requests & Game Queue
* Adding Song Added & Song Played commands that are run for their respective situations
* Moving Song Requests settings into dedicated Settings menu
* Adding Sub Priority option for Song Requests
* Adding option for max requests user for Song Requests
* Adding option to save Song Request queue on exit
* Adding the ability to move Song Requests up & down in list
* Adding the ability to ban song requests via UI & Song Request action
* Adding User Joined & User Selected Game Queue commands that are run for their respective situations
* Adding the ability to specify a Max Host Length for Auto Hoster
* Creating separate enable/disble option for Game Queue action
* Adding the ability to target a user for certain Game Queue actions
* Adding $songtitle, $songalbumimage, & $songuser\_\_\_ Special Identifiers
* Adding $spotifysongtitle & $spotifysongalbumimage Special Identifiers
* Adding Remove Random & Remove Specific Line From File options to File Action
* Adding $argX:Ytext range Special Identifier
* Adding $randomnumberX:Y range Special Identifier
* Adding display of Fan Progression on Users editor window
* Adding new updater & installer application
* Various quality of life & bug fixes

## v0.5.0.5

* BREAKING CHANGE: Bot Accounts & Moderators will need to re-authenticate their accounts due to a change with Mixer's authentication system
* Various quality of life & bug fixes

## v0.5.0.4

* Fixing bug with Channel Fan Progress Level-Up event not triggering, requires deleting & re-adding the command to work properly
* Adding display of fan progress level on Chat User pop-up
* Various quality of life & bug fixes

## v0.5.0.3

* Various quality of life & bug fixes

## v0.5.0.2

* Adding Channel Fan Progress Level-Up event command
* Adding $userfanprogression Special Identifiers
* Adding age rating requirement & host ordering options to Auto-Hoster
* Various quality of life & bug fixes

## v0.5.0.1

* Fixing bug with auto hoster not stopping after first host

## v0.5.0.0

* Official launch of Mix It Up Remote application for iOS & Android
* Adding stand-alone Auto-Hoster application for the automatic hosting of channels when you are offline
* Adding Channel Subscription Gifted event command
* Adding Super Animal Royale Drop Map MixPlay game
* Updating Mixer Paint MixPlay game to allow saving & sharing of drawings
* Adding pop-out GIF image when a GIF skill is used
* Adding MixPlay Settings option to only allow cooldowns if they are larger than the current cooldown on a control
* Adding Enable/Disable Control option to MixPlay actions
* Adding the ability to update several more properties via the Update Control option for MixPlay actions
* Adding Un-Ban User option to Moderation Action
* Adding the ability to specify a moderation reason when using the Add Moderation Strike option for the Moderation action
* Adding "Emotes & Skills Only" Chat/MixPlay moderation rule
* Adding role exemption for Chat/MixPlay moderation rule
* Adding "Set Scene Collection" option to Streaming Software Action
* Integrating YouTube song request play-back directly into the main application & out of the overlay
* Small improvements to YouTube & Spotify song request reliability
* Adding "Mouse/Key Held" input trigger for MixPlay Button Commands
* Combining the down & up MixPlay input triggers for mouse & key
* Adding preventative measures to try to reduce the chance of MixPlay spam before a control is cooled down
* Adding $dayoftheweek Special Identifier
* Various quality of life & bug fixes

## v0.4.21.9

* Updating XSplit extension framework to work with newest version of XSplit

## v0.4.21.8

* Fixing crash related to missing color scheme for Global Mods

## v0.4.21.7

* Various quality of life & bug fixes

## v0.4.21.6

* Adding tweak to fix Mixer change with emote-triggered chat command
* Various quality of life & bug fixes

## v0.4.21.5

* Various quality of life & bug fixes

## v0.4.21.4

* Various quality of life & bug fixes

## v0.4.21.3

* Adding Vikendi map to PUBG Drop Map MixPlay game
* Various quality of life & bug fixes

## v0.4.21.2

* Adding Apex Legends Drop Map MixPlay Game
* Adding additional options to User details pop-up
* Updating Drop Map images for Fortnite & Realm Royale
* Various quality of life & bug fixes

## v0.4.21.1

* Re-enabling Twitter support; users will need to log out & log back in with their Twitter account
* Adding !setusertitle Pre-Made Chat command
* Various quality of life & bug fixes

## v0.4.21.0

* Adding support for Elgato Stream Deck integration
* Adding support for hot key binding for commands
* Adding the ability to group Chat, Timer, & Action Group commands together. Timer command groups must specify their own time interval at which they are run
* Adding the ability to enable/disable command groups via the Command action
* Adding License Agreement upon logging in
* Various quality of life & bug fixes

## v0.4.20.16

* Re-enabling Twitter service to send tweets, disabling the use of "@"mentions & limiting to 1 tweet every 5 minutes
* Fixing bug with renamed Action Groups not appearing correctly in Action Group Action
* Various quality of life & bug fixes

## v0.4.20.15

* Fixing bug with currency resetting

## v0.4.20.14

* Adding toggle option to Special Identifier actions to specify whether it will work globally or only within that command chain
* Various quality of life & bug fixes

## v0.4.20.13

* Adding $userchannelfeatured Special Identifier
* Adding automatic hiding of Overlay YouTube videos after they have ended
* Various quality of life & bug fixes

## v0.4.20.12

* Various quality of life & bug fixes

## v0.4.20.11

* Various quality of life & bug fixes

## v0.4.20.10

* Updating Twitter connection, users may need to disconnect & reconnect their Twitter accounts
* Adding prevention of tweets that contain more than 1 @Mention per Twitter guidelines
* Various quality of life & bug fixes

## v0.4.20.9

* Adding "User Join Front of Queue" option to Game Queue action
* Various quality of life & bug fixes

## v0.4.20.8

* Various quality of life & bug fixes

## v0.4.20.7

* Adding Patreon Benefit in Settings Usage Requirements to MixPlay commands
* Fixing UI bug with Conditional Actions
* Various quality of life & bug fixes

## v0.4.20.6

* Adding "Between" option to Conditional Action
* Adding $argcount Special Identifier
* Various quality of life & bug fixes

## v0.4.20.5

* Adding Sparks & Embers direct options to Leaderboard Overlay Widget to use Mixer's leaderboard numbers
* Adding quick options to Currency/Ranks to create them based on Sparks & Embers
* Various quality of life & bug fixes

## v0.4.20.4

* BREAKING CHANGE: Removing $embermessage special identifier and adding $skillmessage special identifier
* Allowing Action Groups & Custom Commands to be able to download commands from the Mix It Up store
* Adding $skillcosttype Special Identifier
* Renaming the Channel Sparks/Embers Used events to Channel Sparks/Embers Spent to better associate with what the event does
* Renaming User First Joined event to New User Joined to better reflect when the event triggers
* Various quality of life & bug fixes

## v0.4.20.3

* Adding ember usage tracking to Event List, Progress Bar, Stream Boss, & Timer Train Overlay Widgets
* Adding $embermessage & $topXembersused\_\_\_\_ Special Identifier
* Adding $skillissparks & $skillisembers Special Identifiers
* Updating UI to display Sparks & Embers with new icons
* Various quality of life & bug fixes

## v0.4.20.2

* Adding Channel Embers Used & Chat Message Received events
* Fixing bug with user title ordering determination
* Adding additional methods to Developer API
* Various quality of life & bug fixes

## v0.4.20.1

* Various quality of life & bug fixes

## v0.4.20.0

* Adding new Inventory system to allow users to gain/use/lose items that you define
* Adding new shop system for inventories to buy & sell items
* Adding customizable user titles via the $usertitle Special Identifier that can be defined by roles & the number of months in them or on a per-user basis
* Adding service connectivity with Patreon, TipeeeStream, StreamJar, & TreatStream
* Currencies & rank systems can now define an "Active Rate" for how recent a user has to have participated in the stream to continue to receive it
* Adding the ability to individually delete chat commands when they are run
* Adding option to not delete commands on a per-command basis when delete all chat commands settings option is enabled
* Adding the ability to specify the user role that "Add to all" & "Subtract from all" options for Currency/Rank action apply to
* Adding the ability to specify any number for the $top\_\_\_ Special Identifiers
* Adding $user\_\_\_\_next & $user\_\_\_\_nextrank Special Identifiers for rank systems
* Subscriber & Moderator bonuses for currencies & rank systems are now separated out
* Adding checking command usage requirements first before doing spark transactions
* Various quality of life & bug fixes

## v0.4.19.12

* Adding $streambossuser\_\_\_\_ Special Identifier
* Fixing bug where Apply Strikes toggles on Moderation page are not saving settings when they are toggled
* Various quality of life & bug fixes

## v0.4.19.11

* Tweaking Stream Boss overlay widget to heal the Stream Boss if the person contributing is the current boss
* Tweaking logic for getting total months between two dates
* Various quality of life & bug fixes

## v0.4.19.10

* Adding the ability to test Overlay Widgets that rely on events to update
* Adding "Remove Last Song Requested" option to Song Request action
* Various quality of life & bug fixes

## v0.4.19.9

* Fixing bug where Stream Boss Overlay Widget would reset health if the starting stream boss never changed
* Various quality of life & bug fixes

## v0.4.19.8

* Tweaking cooldown logic for textbox controls to allow them to be cooled down in Mix It Up since they can not currently be cooled down on Mixer
* Adding support for arrays in web request JSON to Special Identifier parsing
* Various quality of life & bug fixes

## v0.4.19.7

* Updating developer API to support running commands with arguments
* Various quality of life & bug fixes

## v0.4.19.6

* Fixing bug where Overlay Actions & Widgets without animations might not refresh properly
* Various quality of life & bug fixes

## v0.4.19.5

* Slightly increasing the delay for checking for updated song request status after a song change has occurred
* Various quality of life & bug fixes

## v0.4.19.4

* Fixing bug where Event List, Stream Boss, & Timer Train Overlay Widgets would count re-follows, re-hosts, & duplicated subs
* Fixing bug where Game Stats Overlay Widget was not properly saving upon closing
* Updates to documentation for developer APIs
* Various quality of life & bug fixes

## v0.4.19.3

* Song Requests Overlay Widget now shows the currently playing song as the 1st entry in the list, regardless of if its from the queue or from the backup playlist
* Various quality of life & bug fixes

## v0.4.19.2

* Fixing bug with Stream Boss Overlay Widget not showing the user image correctly

## v0.4.19.1

* Adding Song Requests Overlay Widget
* Adding "Move All Users To Group" & "Move All Users To Scene" options to MixPlay Action
* Adding the ability to individually apply/disable moderation strikes to the different moderation options
* Adding "Update Name" option to Twitter Action
* The chat message text box on the Chat page now processes Special Identifiers
* Various quality of life & bug fixes

## v0.4.19.0

* Adding new Overlay Widgets feature: <https://github.com/SaviorXTanren/mixer-mixitup/wiki/Overlay-Widgets>
* Adding the ability to create multiple different Overlay endpoints
* Adding the ability to have multiple connections to the same overlay endpoint
* Overlay Video & YouTube actions can now have their volume adjusted
* General Overlay Action improvements & new options for them
* Various quality of life & bug fixes

## v0.4.18.21

* Adding $tweet\_\_\_datetime, $tweet\_\_\_date, & $tweet\_\_\_time Special Identifiers
* Adding switch to disable all timer commands
* Fixing bug where lurking user could not use MixPlay that required certain permissions (EX: Moderators)
* Fixing bug where a Streamer would be alerted they were tagged if their username is contained in another username (EX: Joe => JoeSmoe)

## v0.4.18.20

* Fixing bug where certain MixPlay functionality such as cooldowns were broken due to change to fix control positioning

## v0.4.18.19

* Adding check to Mix It Up Store to prevent downloading commands created in a newer version of Mix It Up

## v0.4.18.18

* Tweaking logic for anonymous & lurking usage of MixPlay
* Tweaking logic for updating controls for MixPlay to prevent controls being moved around

## v0.4.18.17

* Adding $userchannelid, $userchannellive, & $usergameimage Special Identifiers
* Allowing for Timer Minimum Messages & Time Interval to each be 0, but not at the same time
* Fixing bug with anonymous MixPlay users being unable to switch scenes

## v0.4.18.16

* Updating Fortnite Drop Map MixPlay to Season 7 map

## v0.4.18.15

* Tweaking logic with lurking & unknown MixPlay users
* Adding the ability to include an image with Twitter Action
* Various quality of life & bug fixes

## v0.4.18.14

* Updating Skills Catalog Data & and changing loading mechanism to get data from online hosted file to allow for updating data without releasing an update for Mix It Up
* Adding $mixplaycontrolid & $mixplaycontrolcost Special Identifiers
* Various quality of life & bug fixes

## v0.4.18.13

* Special Identifier actions can now process math equations in the Replacement Text box
* Adding $currentsongalbumimage & $nextsongalbumimage Special Identifiers
* Various quality of life & bug fixes

## v0.4.18.12

* Adding background refresh of Chat list to catch missing users & remove gone users
* Fixing bug with GIF skills not displaying correctly on the Chat page when hovering over them
* Fixing bug in Conditional Action when using the Greater Than or Equal To ">=" option

## v0.4.18.11

* Adding additional user refreshing for moderation checks
* Fixing calculation of $donationamountnumberdigits Special Identifier
* Various quality of life & bug fixes

## v0.4.18.10

* Adding performance improvements to scrolling for User list
* Fixing bug with pre-made commands for follow, sub, & Mixer age
* Various quality of life & bug fixes

## v0.4.18.9

* Further removal of user data queries to increase performance
* Fixing GIFs displayed in Chat due to Mixer change
* Adding $streamagerating Special Identifier
* Various quality of life & bug fixes

## v0.4.18.8

* Removing background process for chat users that would sometimes display incorrect chatter numbers & have users gain things while not actually in chat
* Ensuring only chat skills run in that channel are counted as sparks & not chat skills from other channels when in a co-stream

## v0.4.18.7

* Experimental performance improvements to help improve how large hosts are handled
* Adding the ability to set spark costs for all 4 MixPlay drop maps
* Changing Mixer APIs for certain Chat-related services to new one due to existing ones being deprecated
* Updating Skills Catalog data

## v0.4.18.6

* Adding extra logic to correctly detect when the Beach Ball skill is used

## v0.4.18.5

* Adding Channel Sparks Used Event Command
* Adding $timedigits Special Identifier
* Various quality of life & bug fixes

## v0.4.18.4

* Fixing crash related to stickers

## v0.4.18.3

* Adding $skilltype & $skillimage Special Identifiers
* Adding $milestoneremainingamount, $milestonenextremainingamount, & $milestonefinalremainingamount Special Identifiers
* Adding the ability for the File Action read options to work with web-based files
* Various quality of life & bug fixes

## v0.4.18.2

* Window location & sizing is now saved and will be reused when launched again
* Including single settings backup on launching Mix It Up in the event of a file corruption
* Various quality of life & bug fixes

## v0.4.18.1

* Fixing logic for Prevent Unknown MixPlay Users setting to also include new anonymous access to MixPlay controls
* Updating Skills data to include new skills
* Various quality of life & bug fixes

## v0.4.18.0

* Adding chat messages for when a sticker or skill is used
* Adding GIF skill support by showing GIF image on hover of image icon
* Adding event for when a new reward milestone is reached
* Adding event for when a skill is used with $skillname & $skillcost Special Identifiers to be used inside of it
* Adding special identifiers for $userprimaryrole, $milestoneamount, $milestonereward, $milestonenextamount, $milestonenextreward, $milestonefinalamount, $milestonefinalreward, $milestoneearnedamount, $milestoneearnedreward
* Renaming all references of Interactive to MixPlay
* Adding statistics for sparks used on interactive controls & reward milestones
* Recent chat history now appears when the Chat page & pop-out chat is loaded
* Adding Special Identifier processing for Set Custom Metadata option for Interactive Action
* Adding periodic checks to chat list to fix users being removed or kept around in user list due to race conditions
* Extra checks around Mixer disconnection issues to prevent crashes
* Various quality of life & bug fixes

## v0.4.17.5

* Improving random number generation logic for games & other features
* Various quality of life & bug fixes

## v0.4.17.4

* Fixing bug with UI issue when bot account disconnected
* Fixing error with YouTube song request query

## v0.4.17.3

* Fixing crashing bug when using emoticon intellisense on Chat page

## v0.4.17.2

* Fixing crashing bug when using user intellisense on Chat page

## v0.4.17.1

* Fixing bug with username's not being colored correctly in user list

## v0.4.17.0

* Large-scale performance improvements for streams with a large number of users in them
* Adding user & emoticon intellisene pop-ups when typing messages on Chat page
* Whispers sent via the Chat page now appear on the list of messages
* Adding $usersparks & $usermixerage special identifiers
* Adding Nibbles' Carrot Patch & AwkwardTyson Americana themes
* Various quality of life & bug fixes

## v0.4.16.6

* Fixing bug where interactive connection could get caught in a crashing loop

## v0.4.16.5

* Fixing race condition bug where a user leaving to quickly after sending a message can fail to get the user for a message
* Adding exemptions for special accounts for moderation

## v0.4.16.4

* Adding additional diagnostic logging for deleted messages

## v0.4.16.2-3

* Adding settings option for the Update Preview Program

## v0.4.16.1

* Fixing issue with searching for games by name
* Preventing accidently settings overwrite if a user selects "NEW STREAMER" from the login menu, but doesn't log in with a different account when the Mixer authentication window opens
* Adding diagnostic logging around message deletion
* Various quality of life & bug fixes

## v0.4.16.0 - 1 Year Anniversary Update

* Adding new customized themes designed by some of our biggest supports: MyBoomShtick, InsertCoinTheater, Azhtral, DustyThighs, Ahhreggi, & VerbatimT
* Adding Extra Life service connectivity
* Adding user "quick tag" pop-up that appears when you begin typing a username to tag them in a message
* Adding highlight to messages in which you are tagged
* Adding "1 Month Old" & "10 Hours Watched" options for Moderation Participation settings
* Various updates to Developer API
* Various quality of life & bug fixes

## v0.4.15.9

* Fixing bug where symbol & punctuation moderation was looking at the caps for whether to use percentage or minimum checks

## v0.4.15.8

* Fixing detection of incorrect user log in for channel

## v0.4.15.7

* Fixing bug that would freeze/hang UI when chat messages were deleted due to settings or moderation

## v0.4.15.6

* Adding additional diagnostic logging to help diagnose a few issues

## v0.4.15.5

* Adding moderation strikes to user pop-up dialog
* Adding option to reset moderation strikes upon launch
* Adding automatic disabling of repeat for Spotify
* Fixing bug with XSplit extension not connecting properly
* Fixing bug where moderation strikes can roll over when subtracting below 0
* Tweaking moderation logic to not moderate a message if it is a valid command

## v0.4.15.4

* Fixing bug with subscriber role determination

## v0.4.15.3

* Fixing bug where you could not specify a username for adding & removing moderation strikes in the Moderation Action
* Making username optional for Moderation Actions that use it, where it will default to the user that ran the command otherwise
* Tweaking user entrance commands to not trigger on a whisper being their first message

## v0.4.15.2

* Tweaking Overlay hosting to fix issues where some video files would lock up the overlay

## v0.4.15.1

* Fixing crash where removed, pre-made moderation commands that were being referenced could not be found

## v0.4.15.0

* Adding Moderation action to perform chat & interactive timeouts, purges, bans, & much more
* Replaced pre-determined timeouts with new strike system to allow for customizable moderation
* Adding new Moderation option for chat / interaction participation requirement to limit who can interact with stream
* Removing pre-made chat commands for moderation
* Adding Giveaway commands for when a user joins & a winner is selected
* Adding Giveaway option to enter multiple times & set a max number of entries per person
* Adding Giveaway option to not allow a user to win multiple giveaways
* Adding Call of Duty: Black Ops 4 Drop Map to Interactive MixPlay list
* Adding settings option to hide deleted messages
* Adding settings option to save chat & event logs
* Adding the ability to edit a user's viewing hours/minutes
* Adding the ability to edit timestamps for quotes
* Various quality of life & bug fixes

## v0.4.14.8

* Fixing bug where imported ScorpBot commands did not have any actions inside of them

## v0.4.14.7

* Fixing bug where game commands were removing too much currency

## v0.4.14.6

* Tweaking when permissions-based checks are done to prevent spamming of commands
* Fixing bug where custom user commands were not saving edits

## v0.4.14.5

* Fixing bug where YouTube song requests were not being detected as completed

## v0.4.14.4

* Adding support for multi-scene interactive cooldowns
* Preventing cooldown of interactive controls if all usage requirements are not meant (EX: If currency requirement is not met, button/group will not cooldown)
* Improvements to Song Request state detection for playlists, songs, & volume
* Large scale revision/improvement to the Developer API endpoints

## v0.4.14.3

* Fixing bug where users would be unable to log in if automatic backups where enabled, but no folder location was set
* Adding prevention of automatic backups from being enabled until a folder location has been set

## v0.4.14.2

* Fixing bug where some command would get called due to chat triggers that contained other ones inside of them (EX: running "!giveaway" would trigger "!give" instead)

## v0.4.14.1

* Fixing bug where some Event commands had gotten out of order

## v0.4.14.0

* Adding import process for Streamlabs Chat Bot data
* Adding support for multi-work chat triggers by using the semi-colon ";" character to separate them
* Adding automated backup option in Settings to backup your data on a timed interval
* Large-scale rework of the Song Requests system to improve the reliability of songs playing and transitioning correctly (Third times the charm, right?)
* Adding Slot Machine & Lock Box game types
* Adding Serial Action for interacting with Serial IO devices
* Adding information to Chat page to show who has deleted a message & the reason for its deletion of it was auto-moderated
* Adding "Contains" & "<> Contain" options to Conditional Action
* Adding "Reset" option to Currency/Rank Action
* Adding $unicode Special Identifier for displaying Unicode characters
* Removing SoundCloud as a Song Request service due to low usage numbers & inability to function with new Song Request changes
* Improving ScorpBot importing process
* Fixing donation amount bug with GawkBox donations
* Various quality of life & bug fixes

## v0.4.13.9

* Fix issue where subscriber only currencies/ranks wouldn't be awarded
* Various quality of life & bug fixes

## v0.4.13.8

* Fixing bug where moderators could not log in

## v0.4.13.7

* Sound action file paths now support Special Identifiers
* !setgame now displays the name of the game it has been set to
* An error message will now appear when the currently logged in Mixer account does not match the account in Mix It Up
* The "Global" cooldown type is now called "Individual"
* Various quality of life & bug fixes

## v0.4.13.4-6

* Fixing bug where Interactive scene transitions would not work consistently
* Fixing bug where Overlay images & videos would not display properly

## v0.4.13.3

* Adding option in Currency/Rank editor to reset & retroactively give currency/rank points to all users based on the Online Rate & their total viewing time
* The list of Interactive games is refreshed when an Interactive Shared Project is added in the Settings menu
* Improving loading time for when an Interactive game is loaded on login or through a command
* Fixing bug where arrow keys for Input Action were registering as Num Pad keys
* Various quality of life & bug fixes

## v0.4.13.2

* Fixing bugs with currency checks for Currency Actions & some Game Commands
* Fixing bug where a default Interactive game would not show it's commands in the UI

## v0.4.13.1

* Fixing bug in currency action where currency was not being added to the specified users but added all to the command runner
* Fixing bug where Input Actions were severely delayed

## v0.4.13.0

* Adding pre-built MixPlay Interactive projects: Fortnite Drop Map, PUBG Drop Map, Realm Royale Drop Map, & Mixer Paint
* Currency/Rank exemptions now make the user have 0 for each currency/rank, but give them "infinite" amounts for currency/rank checks & deductions
* Adding optional username to be specified for Move User To Scene & Move User To Group Interactive Actions
* Adding option to Interactive Active to modify the properties of buttons, labels, & text boxes
* Adding option to Interactive Active to set custom metadata on interactive controls
* Adding the ability to add custom Interactive Games that can be shared by other users
* File Actions can not process Special Identifiers for the text supplied to them
* Adding $linebreak, $costreamusers, & $donationamountnumberdigits special identifiers
* Adding notification sound for any chat message
* Adding option to block all un-authenticated interactive users
* Various quality of life & bug fixes

## v0.4.12.11

* Fixing bug where custom filtered words would overwrite custom banned words
* Fixing bug where resetting currency for users would not save out correctly unless the user's data had been activately updated during the stream
* Various quality of life & bug fixes

## v0.4.12.10

* Fixing bug with duel where currency is not being refunded on failed game start
* Fixing bug where Hitman Game allowed users who did not enter to be able to win the game
* Make add & addall auto-created currency commands mod+ only
* Adding reply email for issue reporter
* Various quality of life & bug fixes

## v0.4.12.9

* Changing Command Action option for Enable/Disable command into two separate options
* Fixing bug where some Pro users were not showing as Purple in chat list
* Fixing bug where Command Action would not show the referened command properly if it was disabled
* Fixing bug where Games referenced in a Command Action would not work after the Game had been edited
* Various quality of life & bug fixes

## v0.4.12.8

* Minor bug fix due to a bad code merge

## v0.4.12.7

* Adding Mixer Clips creating OAuth scope to future proof for clip creation
* Various quality of life & bug fixes

## v0.4.12.6

* Adding $donationamountnumber special identifier
* Fixing crashes when using backup YouTube/SoundCloud playlists for Song Requests
* Preventing more general crashes for Song Requests

## v0.4.12.5

* Adding the ability for Counter Actions to use Special Identifiers for their amount
* Counter Actions now work with decimal values (EX: 3.4)

## v0.4.12.4

* Adding new special identifier $gamebettype for Roulette game type
* Fixing bug where Command Actions that use a Pre-Made Chat Command were not loading correctly when the bot is re-launched

## v0.4.12.3

* Making Spotify backup playlists play a random song when they are started
* Fixing bug where Song Requests would not display the default playlist in the UI when it transitioned back to it

## v0.4.12.2

* Fixing bug where playing backup YouTube or SoundCloud playlist would cause a crash

## v0.4.12.1

* Reliability improvements to Song Requests processing & handling
* Setting Mixer Clips Show Info to be enabled by default. Users who updated to 0.4.12.0 before this update will need to manually set this value in their Mixer Clips action.

## v0.4.12.0

* Adding Song Request action option to perform a "Pick First Result" option when performing a text search for Song Requests
* Adding the ability to perform text search Song Requests for YouTube
* Adding UI slider & Song Request action option to control volume of all Song Request services
* Adding user tracking for Song Requests & the ability for a user to remove their last requested song via a Song Request action option
* Adding the ability to set a backup playlist for Song Requests when there are no songs in the queue
* Adding the following Special Identifiers: $currentsongtitle, $currentsongusername, $nextsongtitle, $nextsongusername
* Adding toggle button to not send Mixer Clips info chat message
* Improvements to Mixer Clips download reliability
* Adding warning message for users trying to sign in with Mixer Bot account while in Test Stream mode
* Quotes that are added via chat command now show the number for the quote that was added
* Adding prevention for Overlay text wrapping
* Various quality of life & bug fixes

## v0.4.11.4

* Fixing bug where subscribe events were being shown in resubscribe event and resubscribe event was not visible, but still existed in settings

## v0.4.11.3

* Fixing bug with certain Overlay animations not working

## v0.4.11.2

* Adding "Remove First User Type in Queue" option to Game Queue to grab first user that meets a certain User Role
* Adding $streamfollowcount Speical Identifier
* Allow for Status & Collect commands for Coin Pusher & Volcano games to be run without cooldowns affecting them
* Adding better messaging for Spotify Song Requests to indicate Spotify must be running & at least 1 song played
* Fixing bug where failure to save Mixer Clip can cause commands to get locked up & stop responding
* Removing 8 Overlay animations that did not work and subsequently fixing occasional freezing that occurs of Overlay items when selecting Random
* Fixing bug with Discord server information not being saved and failing to connect on re-launch
* Fixing number display rounding for Statistics averages

## v0.4.11.1

* Fixing bug with Game Usage Requirements not saving

## v0.4.11.0

* ### This update will remove all existing Games. If you want to save/export any information from your current Games, please decline this update, save/export your data, and relaunch Mix It Up to get the update

* Large Games overhaul that adds 12 new game types and an easier UI for building & updating your games
* Large UI rework of Currency & Rank editor window and the ability to convert currencies to ranks & vice-versa
* When creating a new currency or rank, a new option is presented to create 3-4 "starter" commands to work with them
* Adding the Command Action to enable/disable commands & run another command with specific arguments
* Adding Streaming Software option for OBS Studio to use Replay Buffer to capture local clips
* Adding Game Queue Action option to clear the queue
* Adding the following Special Identifiers: $mixerclipurl, $streamtitle, $streamsubcount, & $streamhostcount
* Adding settings option to opt-out of usage & error data tracking
* Changing Threshold Requirement to message all of chat with how many more users are required as opposed to whispering a single user
* Fixing bugs in Game & Stream Title updating on Channels page
* Fixing bug with Subscriber-only permissions not working for Moderators & Channel Editors
* Fixing bug where some command triggers were case-sensitive
* Various quality of life & bug fixes

## v0.4.10.4

* Renaming "Individual" Cooldown type to "Per Person" for better identification
* Fixing bug with Basic Interactive Chat & Sound commands not saving cooldowns correctly when updating

## v0.4.10.3

* Fixing bug where imported ScorpBot chat commands don't have the "!" toggle set for them. If you recently imported from ScorpBot and have this issue, you can reset your settings by closing Mix It Up, going to the folder "%localappdata%/MixItUp/Settings", and deleting all files in there

## v0.4.10.2

* Fixed crash related to previously removed User Timeout event command

## v0.4.10.1

* Possible bug fix for some users that are experiencing crashes when starting stream

## v0.4.10.0

* Adding new Mixer Clips action to allow Partner streamers to create & download clips through Mix It Up
* Adding Streamlabs action to interact with Streamlabs overlay functionality
* Adding exporting of Statistics information to spreadsheet file
* Adding generic User data importing via plain-text file or spreadsheet file
* Adding option to Web Request action to parse JSON result into Special Identifiers
* Adding filtering options for Chat commands & Action Groups
* Adding Stream Start & End event commands
* The arrows keys on the Chat UI now allow you to go back and forth between recent chat text you have entered
* Adding Special Identifier processing for Wait Actions
* Adding locking for individual commands to ensure no extra uses slip in between Requirement checks and running the actual command
* Disabling the "!ban" pre-made command by default
* Removing User Timeout event command as it is not possible for the Streamer to get this event when another user is timed out
* Various quality of life & bug fixes

## v0.4.9.3

* Fixing bug with Bot authentication & adding additional help text for new users
* Fixing bug disabled timers still running
* Adding duplicate User Host checking to prevent spam

## v0.4.9.2

* Fixing bug preventing store command uploads
* Fixing bug with donation amounts having excess amount of zeroes

## v0.4.9.1

* Fixing crashing bug on XSplit disconnection

## v0.4.9.0

* Official launch of the Mix It Up Store for download
* Tweaking Currency Action UI & removing chat message functionality from it to simplify action
* Adding Export User Data option in Users area to create a tab-delimited data file
* Fixing sorting issues in Users area not sorting by all users instead of just the currently displayed users
* Adding $featuredchannels special identifier
* Adding incrementally increase z-index for Overlay elements to avoid graphical issues
* Removing "!xbox" trigger from Xbox Game pre-made chat command
* Various quality of life & bug fixes

## v0.4.8.1

* Improvements to reconnection logic
* Bug fix for currency/rank not being acquired
* Fixing issue with decimal values for donations/tips
* Removing text requirement for Save To File & Append To File option for File Action
* Various quality of life & bug fixes

## v0.4.8.0

* Adding Top 10 special identifiers for hours, currency, and rank
* Adding support for seperate offline rate for currency & rank
* Changing giveaway times to be in minutes instead of seconds
* Adding option to disable giveaway "!claim" command
* Adding option for giveaway reminder time
* Making user profile images circular in chat
* Adding tooltips for emote text
* Making cooldown groups distinct
* Various quality of life & bug fixes

## v0.4.7.4

* Bug fix for !xboxgame not responding correctly to entered game text

## v0.4.7.2

* Bug fix for new Whisper Number Tracking incorrectly whispering regular chat messages

## v0.4.7.1

* Fixing bug where certain actions were not correctly being added when selected

## v0.4.7.0

* Adding integration with Tiltify
* Adding support for donation-based giveaways
* Adding support for sound output selection for defaulting all sounds & individually per Sound Action
* Adding support for new Threshold command usage requirement that requires a certain number of unique users to use command before its run
* Adding the option to give currency to all users in chat and deduct currency from user in Currency Action
* Adding new simple Overlay action positioning & tweaking UI
* Adding Overlay Web Page sub-action
* Adding the ability to edit quotes
* Adding !xboxgame command and integrating Xbox game searching into !game command
* Adding new "Run One Randomly" option to Action Groups to allow one action to be run at random
* Adding settings option to auto-connect to an Interactive game
* Adding settings option to unban all users from channel
* Adding settings option to track & assign whisper numbers to Users who whisper you
* Various quality of life & bug fixes

## v0.4.6.0

* Re-working Song Request feature & adding support for YouTube & SoundCloud
* Merging OBS Studio, XSplit, & Streamlabs actions into Streaming Software action
* Adding support for stream start & end option for Streaming Software action
* Adding support for random user selection in Game Queue
* Adding support for hosting another channel from Favorites listing
* Adding support for enabling & disabling Game Queue & Song Requests via their respective actions
* Adding support for connecting & disconnecting Interactive games in the Interactive action
* Command play buttons now ignore cooldowns
* Adding support for assigning custom labels to each action for organization
* Adding 5 minute reminders for all Giveaways longer than 5 minutes
* Adding wildcard support for Moderation filtered & banned words
* Adding Mixer user role exemption for Moderation timeouts
* Various quality of life & bug fixes

## v0.4.5.7

* Fixing bug with Input Actions not saving correctly
* Fixing bug with where bot crashes if GameWisp API is down or unavailable
* Fixing bug with where bot crashes if user in chat has no roles
* Fixing bug with where bot crashes if request for current Interactive users fails
* Various quality of life & bug fixes

## v0.4.5.6

* Fixing bug with Streamlabs donations triggering correctly

## v0.4.5.5

* User entrance commands now only run after the user says something in chat
* Fixing bug with crashes due to Interactive user failed calls
* Fixing bug with Spotify song requests when using song link
* Fixing bug with user-only chat commands not saving when using basic chat/sound command creation

## v0.4.5.4

* Fixing issues with Event Command buttons not working in UI

## v0.4.5.0

* Adding new event commands & separating out donation event commands
* Adding new $randomfollower\_\_\_ & $randomsub\_\_\_ Special Identifiers
* New settings options to auto-delete chat commands, hide viewer counts, and whisper alerts to Streamer account
* Custom Special Identifiers are now passed on other commands/action groups that are run
* Adding new crash & issue reporting application
* Various quality of life & bug fixes

## v0.4.4.1

* Reliability improvements for getting Interactive users
* Adding URL encoding for all Special Identifiers used in a Web Request action
* Fixing bug with currency, rank, and spark exemptions now saving for users
* Various quality of life & bug fixes

## v0.4.4.0

* Adding support for Interactive Joysticks & Text Boxes
* Adding Mixer Status Alerts to Login window
* Adding random option for Overlay action animations
* Adding Channel Editor & Partner Mixer user roles
* Adding $targetuser\_\_\_\_ Special Identifier to simplify commands
* Various quality of life & bug fixes

## v0.4.3.2

* Fixing bug with Follower user role not being detected correctly

## v0.4.3.1

* Fixing bug with user roles not being set correctly
* Various quality of life & bug fixes

## v0.4.3.0

* Adding GameWisp support for user role requirement & subscribe events
* Interactive commands now have access to all Usage Requirement settings
* Adding new Group cooldown type to allow grouping of commands for cooldown
* Large reliability improvements to general Interactive functionality
* Adding options to wipe individual and all user data
* Adding new "$\_\_\_\_followers" Special Identifiers
* Various quality of life & bug fixes

## v0.4.2.3

* Adding new Special Identifiers for stream start time and uptime
* Adding !setaudience Pre-Made Chat Command
* Fixing bug in File Action Line Reads

## v0.4.2.2

* Possible fix to CPU Maxing issue some users are experiencing on long streams

## v0.4.2.1

* Fixing bug with Streamlabs donations not being detected
* Fixing bug with Games not responding to their chat commands

## v0.4.2.0

* Streamlabs OBS integration completed
* Fixed crashing bug when no Bot account is linked, but ignore Bot commands is enabled
* Various quality of life & bug fixes

## v0.4.1.0

* Adding new Users section to allow editing of User data, including Custom Commands & User-specific settings
* Adding $streamer\_\_\_\_\_\_ Special Identifiers
* Adding $randomnumber\_\_\_\_\_ Special Identifier
* Fixing bug where Interactive Button Background Images were being removed
* Various quality of life & bug fixes

## v0.4.0.0 - Sigma Release

* New Settings menu to store all application-wide settings
* New installer executable & default installation location migration
* Large overhaul of Overlay Action layout
* Adding new Overlay Action animations
* Adding Play button to individual actions to test them out
* Adding Conditional action
* More ScorpBot variable conversion support
* Various quality of life & bug fixes

## v0.3.19.0

* Adding GawkBox service support
* Adding additional options for File Action
* Adding new $random\_\_\_\_ Special Identifiers
* Adding "quick command" creation option when a new Currency or Rank is created
* Various quality of life & bug fixes

## v0.3.18.0

* Adding Translation action
* Adding Twitter action
* Fixing bug with getting access code for Remote connection
* Various quality of life & bug fixes

## v0.3.17.3

* Large feature update to Text to Speech action
* Adding new Mixer Service alerts to notify about disconnections
* Fixing issue with "!claim" command for Giveaways
* Various quality of life & bug fixes

## v0.3.17.2

* Fixing authentication token issue with Discord service
* Improvement to Dark Theme colors for Chat & Users
* Adding Spotify support to Developer API
* Various quality of life & bug fixes

## v0.3.17.1

* Bug fix for Currency/Rank saving
* Bug fix for Currency/Rank importing
* Bug fixes for Spotify & Song Request services

## v0.3.17.0

* Adding Discord integration
* Adding improvements to service reconnection (EX: Chat, Interactive, etc)
* Various quality of life & bug fixes

## v0.3.16.5

* Fixing bug with giveaway command not working
* Various quality of life & bug fixes

## v0.3.16.4

* Adding the option to unlock commands to allow them to run without blocking other commands
* Allowing currency amount to be a special identifier
* Adding option to specify voice for Text To Speech action
* Fixing import issue of currency/ranks from Streamlabs-OBS
* Various quality of life & bug fixes

## v0.3.16.3

* Large usability & reliability improvements to Overlay & XSplit services
* Fixing song selection & play/pause functionality for Song Request feature
* Fixing issues with Remote commands & groups
* Various quality of life & bug fixes

## v0.3.16.2

* Adding Play/Pause & Next buttons to Song Requests area
* Fixing bug with Remote service connections
* Various quality of life & bug fixes

## v0.3.16.1

* Adding the ability to create Chat commands without an "!"
* Whispering a command to the bot account will now run that command
* Fixing bug with Application settings not persisting with updates (EX: Dark Theme)

## v0.3.16.0

* Adding Song Request feature with Spotify support
* Adding Spotify integration for controlling your Spotify player
* Adding sorting by name for Command listings in Action Groups, Remote Commands, and other areas
* Resetting Counter save files if the "Reset On Load" option is enabled
* Adding quick link for Counters file location
* Adding better save file checking & backup file use if regular settings fails
* Various quality of life & bug fixes

## v0.3.15.5

* Adding additional logging to help diagnose some harder to discover issues
* Various quality of life & bug fixes

## v0.3.15.2

* Fixing issue with random crashes due to Chat connectivity
* Fixing issue with Interactive games not loading correctly if a button does not have a spark cost
* Fixing bug with Interactive connectivity for some users
* Various quality of life & bug fixes

## v0.3.15.1

* Adding Favorite Users/Teams feature in Channels area
* Fixing bug with Interactive connectivity for some users

## v0.3.15

* Adding integration for Streamlabs donation tracking
* Adding integration for Twitter tweet lookups
* Adding support for mp4 Á webm videos for Overlay Action
* Adding option for Currency Action to specific user to receive
* Adding pre-made "!costream" chat command for showcasing who is in the costream
* Various quality of life & bug fixes

## v0.3.14

* Updating ordering of requirement checks and currency subtraction for commands with permissions
* Adding Chat scroll Lock/Unlock functionality
* Making chat alerts easier to see in Dark theme
* Fixing !uptime command by using new Mixer API
* Removing duplicated users from database, possibly fixing issue with users not receiving minutes and currency
* Various quality of life & bug fixes

## v0.3.13.1

* Fixing crash when attempting to use existing games. This fix unfortunately might default the Currency Requirement in your Game to 1, so you will need to check & possibly update your game after this update.
* Fixing crash when attempting to delete quotes
* Various quality of life & bug fixes

## v0.3.13

* Adding the ability to set cooldowns on a global or individual user basis
* Adding alert message in Mix It Up chat window for chat user join/leave, events, and interactive
* Adding support for messages that are larger than 360 characters by splitting them into multiple messages
* Adding extra checks for follower & subscriber validation
* Updating the user endpoint for the Developer API
* Fixing bug where user's currency & ranks would get reset on re-following, hosting, subscribing, etc
* Fixing bug with analytics logging
* Various quality of life & bug fixes

## v0.3.12

* Adding new Interactive Action options to cooldown a specific button, cooldown group, or entier scene
* Adding the option to save & read a Counter Action's value to a text file in Counters folder
* Adding support for Youtube Video display on Mix It Up Overlay
* Large UI & reliability improvements to Find Channel To Raid feature, including Team searching
* Adding new File Action to allow for saving text out to a file and reading text from a file into a custom Special Identifier
* Adding new !addcommand, !updatecommand, & !disablecommand Pre-Made Chat Commands for Streamer command management by Moderators
* Adding Font Size selector on Chat window
* Various quality of life & bug fixes

## v0.3.11.1

* Fixing bug with Creating Basic Chat & Sound commands
* Fixing bug Chat pop-up crashing bug

## v0.3.11

* Adding new Usage Requirements UI for Chat Commands, Games, Giveaways, & Game Queue
* Fixing bug with Emote moderation detection
* Removing save button from Moderation & performing automatic saving instead
* Adding extra checks for follow & subscriber checks to ensure newly followed & subscribed users are included
* Various quality of life & bug fixes

## v0.3.10

* Large feature changes and improvements to the Moderation service
* Adding support for importing moderation settings from ScorpBot
* Adding statistics tracking for unfollows, subscribes, and resubscribes
* Various quality of life & bug fixes

## v0.3.9

* Users can now look at the other scenes of the interactive game they are connected to (This will not change what scene is displayed to users)
* Fixing Chat window scrolling issues (Finally!)
* Removing duplicate users from settings
* Adding missing Special Identifier for $usercurrencyname
* Fixing a few crashing bugs

## v0.3.8

* Adding retry connection logic to both Constellation (Events) and Interactive connections
* Fixing issue with Interactive not being able to connect
* Removing duplicate users from settings
* Improving user experience for re-launching the New User Wizard
* Adding additional logging for diagnostic purposes

## v0.3.7

* Changing login authentication port to 8919 to prevent login issues people were having when FireBot was running at the same time
* Fixing crashing bug with new Statistics feature

## v0.3.6

* Adding experimental Statistics feature. This feature is not currently complete and should not be relied upon for accuracy currently. Future updates will stablize and improve this feature
* Adding retry logic around initial login connection as a stop-gap until Mixer API reliability is improved
* Adding new Special Identifier Actions for creating custom Special Identifiers
* Adding "Move user to scene" option and tweaking existing options for Interactive Actions
* Adding new option for Web Request Action to set result into a custom Special Identifier for later use
* Adding currently played game & timestamp to all future quotes
* Auto-correcting issues with Pre-Made Game's chat commands not being whispered
* Adding additional references to Help links in new user wizard
* Removing instances of deprecated $usercurrencyname Special Identifier
* Various quality of life & bug fixes

## v0.3.5

* Adding the ability to resize the User list in Chat
* Adding new UI options to view and edit a user's Currency & Rank
* Adding new update UI to fix issues some people were experiencing where they could not perform an update (this will be visible on the NEXT update)
* Making !uptime command more resilient by listening for stream start & end events to assist until stream sessions API is more reliable
* Adding simplier visual indicator for Currency & Ranks if they are based on minutes, hours, or a custom rate
* Fixing issue with Timer commands where they were not being run
* Fixing & improving ScorpBot importing around Currency & Rank, now allowing for rank importing based on hours while still importing points
* Fixing issue with Game's not correctly whispering users when they are supposed to (You will need to delete any Pre-Made games and re-create them to see this fix)
* Various quality of life & bug fixes

## v0.3.4

* Fixing bug with latest version of OBS Studio when trying to use OBS Web Browser action
* Removing boomtvmod from Currency & Rank accumulation

## v0.3.3

* Overlay actions are now run together in sets
* Adding better error handling for Overlay & XSplit connectivity
* Adding new $hostviewercount special identifier
* Adding the ability to specify who is whispered for Chat actions
* Fixing issues with Chat scrolling
* Various quality of life & bug fixes

## v0.3.2

* Adding confirmation prompts to deleting commands and closing the app
* Adding additional info to About section
* Removing bots from currency & rank accumulation
* Adding Game chat triggers to display list
* Fixing issues with Chat scrolling
* Updating "!commands" Pre-Made command to list out all commands possible in chat by that user and moving old functionality into "!mixitupcommands"
* Fixing bug with ScorpBot data importing
* Various quality of life & bug fixes

## v0.3.1

* Adding additional options to Interactive Actions
* Adding special identifier for game that user was playing
* Adding currency maximum amount
* Updating information for OBS Studio connectivity
* Various quality of life & bug fixes

## v0.3.0

* Large performance updates through the entire application
* New "Basic" command system allows for easy chat or sound commands for Chat, Interactive, Events & Timers
* The ability to re-run the New User Wizard and several improvements to the importing of ScorpBot data
* New Games system containing both pre-made and customizable game creation
* Large re-working of Currency & Rank system to simplify usage
* New Action Groups to allow of commonly used sets of actions
* Various quality of life & bug fixes

## v0.2.7

* First auto-updatable release
* New User Wizard that allows for importing of ScorpBot & Soundwave Interactive settings
* Large re-work of Overlay & XSplit services for better performance and reliability
* Channel Management feature
* New statistics feature levaraging <http://mixdash.cc>
* Large overhaul of the Currency system
* New Rank system implemented
* Special Identifiers added and existing ones tweaked
* Updates to Game Queue & Giveaway features
* Adding new pre-made chat commands
* Adding database storage for larger data to simplify save & loading of settings
* Various quality of life & bug fixes

## v0.1.1

* Various bug fixes for Pre-Release Alpha

## v0.1.0

* Initial Pre-Release Alpha
