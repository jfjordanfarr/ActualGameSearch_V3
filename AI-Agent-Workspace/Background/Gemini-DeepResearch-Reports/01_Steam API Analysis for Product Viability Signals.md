
## User Prompt

I am a developer who has released more than one game on Steam and has a valid Steam API key.



In the past, I have done exploratory work, research, and even a bit of commercial prototyping, for a search engine for steam games derived from semantic search over game descriptions and reviews, re-ranked with factors present in the metadata.



One factor I'm not certain I have access to, in terms of measurement, is the ability to detect therough the API whether a steam app has received updates, of what type, and how many. Can you help me understand what data is available to me in terms of accessing game update count, type, and text?



In addition, what signals are available to me for review responses/reply threads? Updates to reviews? That one isn't quite as salient but my overall aim is to have a handy set of metrics which can help distinguish good-faith products from shovelware carrying promoted reviews. Total playtime has been a useful metric there, but I'm just trying to assess what data is available to me to help make those distinguishments.

---

## Gemini Deep Research

Here's the plan I've put together. Let me know if you need any changes before I start researching.
```
 Steam API Game Update Metrics 
Research Websites
(1) Locate and review the official Steamworks Web API documentation, specifically focusing on the interfaces and endpoints related to application news, user-generated content, and reviews.
(2) Investigate the `ISteamNews` interface, particularly the `GetNewsForApp` method. Analyze the structure of the data it returns to determine if it provides explicit information on update types, a total count of updates, or if this information must be inferred from the title and content of the news items.
(3) Examine the API endpoints responsible for fetching user reviews. Scrutinize the returned data for each review to identify any fields or objects that contain developer responses, review edit histories, or associated comment threads.
(4) Search through developer forums, game development communities, and programming resource websites for discussions and guides on using the Steam API to track game patches and developer-player interactions. Identify common practices, known limitations, and community-devised workarounds.
(5) Synthesize the findings into a comprehensive list of potential metrics for evaluating a game's post-launch support and community engagement. This should include:
(a) Directly available data points (e.g., frequency of news posts, review scores).
(b) Inferred metrics (e.g., classifying news items as 'major update', 'hotfix', or 'community post' based on content analysis).
(6) Compare the data available for game updates (via `ISteamNews`) with the data available for community workshop updates or other app-related changes to understand the scope of the API's tracking capabilities.
(7) Formulate a summary of the key data points available versus those that are unavailable through the API for distinguishing 'shovelware' from actively maintained games, highlighting any significant gaps in the available data.
Analyze Results
Create Report
```

---

# **Report 01**
# **Steam API Analysis for Product Viability Signals**

## **I. Tracking Application Updates: An In-Depth Analysis of the ISteamNews API**

The primary, albeit imperfect, mechanism for programmatically tracking developer updates is the ISteamNews interface within the Steamworks Web API. A thorough analysis of this interface, including its documented and undocumented parameters, allows for the construction of a robust methodology to distinguish substantive product updates from other forms of developer communication. This is a foundational step in identifying developers who are actively maintaining and improving their products post-launch.

### **1.1. The GetNewsForApp Endpoint: Foundational Mechanics**

The designated method for retrieving all news and announcements associated with a specific application is the GetNewsForApp endpoint.1 The standard request is a

GET call to a URI structured as follows: http://api.steampowered.com/ISteamNews/GetNewsForApp/v2/.3 It is critical to explicitly specify the version number (e.g.,

v2 or v0002) in the request URI. The Steam Web API can and does introduce new versions of methods that may contain breaking changes; pinning to a specific version ensures the long-term stability and predictability of the data ingestion pipeline against unannounced API modifications.6

The endpoint accepts several core parameters to control the scope of the query. The appid is a required unsigned 32-bit integer identifying the target application. Other optional parameters include count to specify the number of posts to retrieve (defaulting to 20), maxlength to control the length of the returned content, and enddate (a Unix timestamp) to retrieve posts published before a specific time.3

The response is delivered in JSON format by default and is encapsulated within a root appnews object. This object contains the appid of the request and a newsitems array. Each element within this array represents a single news post and provides key metadata: gid (a unique post identifier), title, url, author, date (as a Unix timestamp), and contents.5 This structured data forms the raw material for any system designed to track the frequency and content of developer updates.

### **1.2. Filtering for Update Type: The Undocumented tags Parameter**

The most significant capability for distinguishing between update types lies in an optional, yet officially undocumented, parameter: tags. Community-driven analysis and inspection of the API's self-documenting features reveal that passing tags=patchnotes filters the results to exclusively return formal patch notes, separating them from all other announcement categories such as sales, community events, or marketing materials.3 This parameter is the single most effective tool for addressing the core objective of identifying substantive product updates.

The existence of this parameter can be programmatically verified by querying the ISteamWebAPIUtil/GetSupportedAPIList endpoint. The response from this utility method details the accepted parameters for all available API calls, and for GetNewsForApp, it explicitly lists tags as a valid string parameter.3 This discrepancy between the official partner documentation and the API's actual capabilities underscores a critical characteristic of the Steam API ecosystem: official documentation is often incomplete, and programmatic exploration combined with community-sourced knowledge is essential for unlocking the API's full potential.

In addition to the tags parameter, the official documentation does mention a feeds parameter, described as a "Comma-separated list of feed names to return news for".7 Community examples have shown this parameter being used with values such as

steam\_community\_announcements.3 While potentially useful for broader categorization—for instance, separating official developer posts from other syndicated content—it offers a much less precise filter than

tags=patchnotes. For the specific goal of isolating product updates, the tags parameter is the superior and recommended instrument.

### **1.3. Navigating Data Quality and API Limitations**

While the ISteamNews interface provides the necessary data, it presents significant challenges in terms of data quality and completeness. The contents field of each news item is returned as a single string containing an unstructured mix of plain text, HTML markup, and proprietary Steam BBCode (e.g., {STEAM\_CLAN\_IMAGE} for embedding clan images).8 This necessitates a considerable investment in server-side post-processing. A robust data ingestion pipeline must employ complex regular expressions or a dedicated BBCode-to-HTML/text parser to sanitize this content before it can be used for reliable semantic analysis.

The maxlength parameter exhibits anomalous, undocumented behavior that can be leveraged as a preliminary sanitization step. While setting maxlength=0 returns the full, unprocessed content, setting it to its maximum unsigned 32-bit integer value (4294967295) has the curious side effect of stripping most, but not all, BBCode and HTML tags from the response.8 This can be a useful first pass, but it is not a complete solution, as it can also disrupt formatting, such as removing newlines, and may not handle all edge cases. A comprehensive regex-based sanitization layer remains the most reliable approach.

Furthermore, the API is widely regarded by the developer community as outdated.8 It conspicuously fails to provide structured data for rich media elements that are clearly visible on the corresponding Steam news web pages, such as post subtitles, executive summaries, and header/cover images. This limitation means that any automated analysis must be derived purely from the raw, sanitized text of the

contents field. The inability to, for example, programmatically identify posts containing embedded videos or specific image types is a notable constraint. The well-documented disparity between the limited data available via the API and the richer, more structured information available in the corresponding RSS feeds for the same news items confirms this is a known and long-standing deficiency of the interface.8

### **1.4. Second and Third-Order Implications for Analysis**

The mechanics and limitations of the ISteamNews API give rise to several strategic considerations for building a sophisticated analysis platform. A simple count of news posts is a naive metric; a far more powerful signal of developer commitment can be derived by calculating a "Patch Notes Ratio." This involves making two distinct API calls for the same appid: one with the tags=patchnotes filter and one without. The ratio of the count of patch notes to the total count of announcements provides a quantitative measure of a developer's focus. A high ratio suggests a team dedicated to product iteration, bug fixing, and transparent communication about the game's evolution. Conversely, a low ratio, particularly one dominated by marketing announcements or low-effort community posts, can serve as a red flag for a product that is not receiving active development support post-launch. This metric elevates the analysis from a simple measure of activity to a qualitative assessment of the *nature* of that activity.

The process of discovering the tags parameter itself reveals a fundamental truth about working with Valve's APIs. The fact that the most critical parameter for this use case is absent from the official partner documentation but is discoverable through community forums and programmatic API self-discovery (GetSupportedAPIList) indicates that Valve's documentation should be treated as a starting point, not an exhaustive reference. A successful, long-term data strategy must institutionalize the monitoring of community resources like Stack Overflow and Reddit, as well as periodic programmatic re-evaluation of API capabilities. This approach acknowledges that the API's feature set may be broader and more nuanced than what is officially stated.

Finally, the technical challenge of sanitizing the contents field should be viewed not merely as an implementation hurdle but as a potential competitive advantage. The significant engineering effort required to build a reliable parser for Steam's mix of HTML and proprietary BBCode acts as a technical barrier to entry. Competing services may be deterred by this complexity, or they may proceed with raw, noisy data, leading to inferior semantic analysis and search results. By investing in a robust sanitization library, a search engine can achieve a markedly higher quality of data refinement. This superior data quality will directly translate to more accurate search, better categorization, and a more reliable assessment of developer activity, creating a distinct and defensible advantage in the market.

## **II. Deconstructing Review Dynamics: Signals of Engagement and Product Evolution**

Beyond developer-pushed updates, the dynamics of user reviews provide a rich, multi-faceted dataset for assessing product health and developer engagement. A non-obvious but officially supported Storefront API endpoint provides access to a highly structured corpus of review data, enabling the direct measurement of developer responsiveness, the evolution of player sentiment over time, and community engagement levels.

### **2.1. Accessing the Review Corpus: The appreviews Endpoint**

The primary and most comprehensive method for programmatically fetching user reviews is not part of the standard api.steampowered.com Web API suite. Instead, it is an endpoint on the Storefront domain, structured as follows: https://store.steampowered.com/appreviews/\<appid\>?json=1.9 This architectural distinction is critical, as it is governed by different access patterns and is not listed in the main Web API reference.

This endpoint is designed to handle the massive volume of reviews for popular games and therefore utilizes cursor-based pagination for comprehensive data retrieval. To fetch the complete review set, the initial request must include the parameter cursor=\*. The JSON response for that request will contain a new cursor value, which must be URL-encoded and then passed as the cursor parameter in the subsequent request to retrieve the next batch of reviews.9 This process is repeated until the response no longer contains a

cursor field, indicating that the end of the review set has been reached.

The endpoint also supports a powerful set of filtering parameters that allow for highly specific data queries. These include language (using API language codes), review\_type (to filter for positive or negative reviews), purchase\_type (to distinguish between steam and non\_steam\_purchase reviews), and num\_per\_page (which can be set to a maximum of 100 to reduce the total number of required API calls).12

The JSON response from this endpoint is exceptionally well-structured and directly provides the data needed to answer the core questions about review dynamics. For each review, the response includes fields such as recommendationid, review (the text content), voted\_up (boolean), votes\_up, and comment\_count. Crucially, it also contains an author object with detailed playtime statistics (playtime\_forever, playtime\_at\_review), and, most importantly, the specific fields developer\_response (the text of the developer's reply), timestamp\_dev\_responded, timestamp\_created, and timestamp\_updated.12

### **2.2. Quantifying Developer-Player Interaction**

The presence of the developer\_response (string) and timestamp\_dev\_responded (Unix timestamp) fields in the API response provides a direct and unambiguous signal of developer engagement with player feedback.12 This data allows for the creation of several key performance indicators for developer responsiveness. One can programmatically calculate the percentage of negative reviews that receive an official developer response, the average time-to-response (the delta between

timestamp\_created and timestamp\_dev\_responded), and perform sentiment analysis on the developer\_response text itself to gauge its tone and helpfulness.

Valve's own best-practice documentation for developers provides valuable context for interpreting this data. Developers are encouraged to use the response feature to clarify missing information or to notify a reviewer that a bug they encountered has since been resolved. However, they are explicitly warned against using it to argue with reviewers or refute customer opinions, as this is often perceived as defensive and counter-productive.14 Therefore, a pattern of concise, informative, and empathetic responses is a strong positive signal of a healthy developer-community relationship. Conversely, a pattern of argumentative, dismissive, or generic "canned" responses can be a significant red flag, indicating a poor company culture or a simple desire to perform superficial damage control.15

### **2.3. Mapping the Lifecycle of a Review**

The API's provision of both timestamp\_created and timestamp\_updated for each review is a powerful feature that enables the tracking of a review's evolution over time.12 Any review where the

timestamp\_updated value is greater than the timestamp\_created value is one that has been edited by its author. This data point, on its own, is interesting, but its true analytical power is unlocked when correlated with update data from the ISteamNews API.

This combined dataset allows for the construction of a sophisticated analytical model to measure the direct impact of developer updates on player sentiment. For example, one can identify a cohort of all negative reviews (voted\_up: false) posted in the period preceding a major patch announcement (identified via ISteamNews with tags=patchnotes). After the patch release date, this cohort can be monitored to track how many of those specific reviews are subsequently updated. A change in the voted\_up status from false to true in these updated reviews provides direct, quantitative evidence that the developer has successfully identified, addressed, and resolved key player frustrations. This creates a closed-loop feedback model, moving from player complaint to developer action to validated sentiment improvement.

### **2.4. Community Discussion and Limitations**

The appreviews endpoint provides a comment\_count field, which is an integer representing the total number of comments posted by other users on a given review.12 This metric can serve as a useful proxy for the level of community engagement or controversy a particular review has generated. A negative review with an exceptionally high comment count may indicate a widely shared grievance or a particularly well-articulated critique that resonates with a large portion of the player base.

However, it is imperative to recognize a critical limitation of the API: it provides the *count* of comments but **does not** provide access to the content of the comment threads themselves. The research yields no evidence of any public API endpoint for retrieving review comments. This is almost certainly a deliberate design choice by Valve, likely to protect user privacy and prevent the API from being used for harassment or data scraping of user-to-user conversations. Consequently, any analysis is restricted to the volume and velocity of community discussion around a review, not the substance or sentiment of that discussion.

### **2.5. Inferring Product Health from Evolving Data**

The rich, temporal data provided by the appreviews endpoint allows for the derivation of several advanced metrics that serve as powerful indicators of product health and developer integrity. A sudden spike in the rate of review updates immediately following a patch release—a metric that can be termed "Review Update Velocity"—is a leading indicator of a successful turnaround. When this velocity is further filtered to identify negative reviews that are changed to positive, it becomes a potent, quantifiable signal that a developer is not just releasing updates, but releasing updates that effectively address community concerns. This measures the *impact* of development work, a far more meaningful signal than the mere existence of a patch note.

Furthermore, by aggregating all developer\_response texts for a given publisher across their entire catalog, it becomes possible to create a "Developer Engagement Profile." This profile moves beyond the analysis of a single game to assess the publisher's overarching community management strategy and culture. Algorithmic analysis of this aggregated text can distinguish between developers who consistently provide detailed, technical, and helpful replies versus those who rely on generic, boilerplate PR statements or, worse, become defensive.15 This provides a nuanced, data-driven assessment of whether a developer genuinely values their community or is simply engaging in reputation management.

Finally, the author object within each review contains a subtle but powerful pair of data points: playtime\_at\_review and playtime\_forever.12 For a review that has been updated, the delta between these two values is profoundly informative. A user who writes a negative review after two hours of play but later updates it to be positive after logging an additional fifty hours tells a story of a game with hidden depth or a steep but rewarding learning curve. Conversely, a user who posts a glowing initial review but updates it to be negative after extensive further play may be signaling issues with endgame content, a lack of replayability, or late-game bugs. Analyzing this playtime delta allows for the weighting of reviews based on the author's evolving and deepening experience with the product, providing a far more sophisticated measure of sentiment than a simple snapshot in time.

## **III. Proxy Metrics and Indirect Signals of a Healthy Product**

To achieve a truly holistic assessment of a product's viability, the analysis must extend beyond direct developer communications and user reviews. The broader Steam ecosystem, particularly the Steam Workshop, can serve as a powerful barometer for community health and long-term engagement. Understanding these proxy signals, as well as the fundamental architectural boundaries of the Steam APIs, is essential for building a comprehensive and realistic data model.

### **3.1. The Steam Workshop as an Ecosystem Barometer**

For games that support user-generated content (UGC), the activity level within their Steam Workshop is a potent proxy signal for a healthy, long-term product. Low-effort "shovelware" titles rarely invest in the robust modding tools and API integration necessary to support a Workshop. Even if they do, the community engagement is typically nonexistent, resulting in a barren or stagnant workshop page. In contrast, a vibrant workshop, characterized by a steady stream of new and updated content from a diverse set of creators, signals a deeply committed player base. It also indicates a developer who has invested significantly in the game's longevity by empowering its community.16

When approaching this analysis, it is crucial to distinguish between the two primary APIs related to UGC. The Steamworks SDK includes the ISteamUGC C++ interface, which is integrated directly into the game client and is used by the game and by modding tools to create, upload, and manage UGC items.16 This is not the tool for server-side analysis. For the purpose of a search engine or data analysis platform, the relevant tools are the server-to-server Steam Web APIs, specifically the

IPublishedFileService and ISteamRemoteStorage interfaces, which provide methods for querying metadata about published Workshop files.2

### **3.2. Querying Workshop Activity via IPublishedFileService**

The primary instrument for a server-side analysis of Workshop activity is the QueryFiles method within the IPublishedFileService interface.20 The endpoint is structured as

GET https://api.steampowered.com/IPublishedFileService/QueryFiles/v1/. This method allows for a comprehensive query of all published Workshop files associated with a given appid. Like the review endpoint, it supports cursor-based pagination to handle large result sets.

The JSON response for a QueryFiles request contains an array of objects, each representing a single Workshop item. While the official documentation for this specific method's response is sparse, related methods and community analysis confirm that each item object contains essential metadata, most importantly Unix timestamps for time\_created and time\_updated.20 By paginating through the entire set of Workshop items for a given application, it is possible to construct a complete timeline of UGC creation and subsequent updates.

From this temporal data, several high-value metrics can be derived to quantify the health of the game's modding ecosystem:

* **UGC Velocity:** The number of new Workshop items created per unit of time (e.g., per month). A consistently high or growing velocity indicates a healthy and active creative community.  
* **UGC Maintenance:** The percentage of existing Workshop items that have received an update within a recent time window (e.g., the last six months). A high maintenance rate suggests that authors are not abandoning their creations and are actively supporting them, which in turn signals a stable and engaged player base consuming that content.  
* **Author Diversity:** The number of unique SteamIDs contributing as authors to the Workshop. A high number of unique authors indicates a broad, decentralized community rather than a small, dedicated clique, which is a sign of a more resilient and accessible modding scene.

A high score across these derived metrics serves as a strong positive signal for a game's long-term health and replayability, factors that are often absent in low-effort titles.

### **3.3. Understanding API Boundaries and Setting Expectations**

A final, critical component of a successful data strategy is a clear understanding of the architectural boundaries of the Steam platform. It is essential to differentiate between the Steam Web API and the Steamworks SDK. The Web API is an HTTP-based interface designed for server-side applications to query public or partner-protected data.1 The Steamworks SDK, by contrast, is a C++ library (with bindings for other languages) that is integrated directly into a game's client executable. It facilitates deep, real-time interaction with the local Steam client for features like achievements, matchmaking, networking, and direct file I/O.19

This distinction is crucial for setting realistic expectations about the granularity of data that can be obtained. A query about detecting "game updates" could be interpreted at the binary or depot level—for example, tracking the manifest ID of a new game build, the specific files changed, or the download size of a patch. This level of detail is managed by the Steam client's content delivery system and is **not** exposed through any public or partner Web API. The ISteamNews feed, specifically posts tagged as patch notes, represents the highest-level, publicly queryable signal that a build update has occurred. Attempting to find a Web API endpoint for depot-level changes would be a fruitless endeavor. Acknowledging this boundary is key to focusing engineering efforts on what is possible and avoiding the pursuit of inaccessible data.

## **IV. Synthesis and Strategic Recommendations**

The successful differentiation of good-faith products from shovelware on the Steam platform requires a multi-faceted, data-driven approach. No single metric is sufficient. Instead, a robust model must be built upon a constellation of signals gathered from disparate API endpoints, each providing a unique perspective on developer activity, community engagement, and product evolution.

### **4.1. The Multi-Signal Approach**

A high-fidelity model for identifying good-faith products should be designed to aggregate and weight a variety of positive signals. The core indicators of a healthy, actively supported game include:

1. **High Update Cadence and Focus:** A consistent history of announcements via the ISteamNews interface, with a significant percentage of those announcements being substantive updates identified by the tags=patchnotes filter.  
2. **Proactive Developer Responsiveness:** A demonstrably high rate of official developer responses to negative user reviews, as measured through the appreviews endpoint, coupled with a helpful and constructive tone in those responses.  
3. **Measurable Impact of Updates:** A clear, quantifiable trend of negative reviews being updated to positive by their authors in the period following a major patch release, indicating that developer efforts are successfully addressing player feedback.  
4. **Vibrant Community Ecosystem:** For applicable titles, a thriving Steam Workshop characterized by high velocity of new content creation, consistent maintenance of existing items, and a diverse author base, as queried via the IPublishedFileService.

Combining these signals creates a holistic profile of a product and its developer that is far more resilient to manipulation (such as promoted reviews) than simpler metrics like total playtime or review score alone.

### **4.2. Key API Endpoints and Engagement Signals**

The following table provides a consolidated, technical reference for the key API endpoints, data fields, and the strategic signals that can be derived from them. This serves as a practical guide for the implementation of the data ingestion and analysis pipelines.

| Signal Category | Primary Endpoint/Method | Key Data Fields / Parameters | Derived Signal | Limitations & Nuances |
| :---- | :---- | :---- | :---- | :---- |
| **Update Frequency & Type** | ISteamNews/GetNewsForApp/v2/ | date, tags=patchnotes | **Patch Notes Ratio:** Differentiates active development from marketing. | Content requires heavy sanitization (BBCode). Undocumented tags parameter is critical. |
| **Developer Responsiveness** | store.steampowered.com/appreviews/ | developer\_response, timestamp\_dev\_responded | **Response Rate & Time:** Measures direct engagement with negative feedback. | Response text requires sentiment analysis for full context. |
| **Impact of Updates** | appreviews & ISteamNews | timestamp\_updated, voted\_up | **Sentiment Turnaround:** Correlates patches with negative reviews changing to positive. | Requires joining data from two separate endpoints. Causal link is inferred, not explicit. |
| **Community Engagement** | appreviews | comment\_count | **Review Controversy/Interest:** High count indicates a review resonates with many users. | Comment content is inaccessible. High count could be positive or negative engagement. |
| **Ecosystem Health (UGC)** | IPublishedFileService/QueryFiles/v1/ | time\_created, time\_updated | **UGC Velocity & Maintenance:** Measures the vibrancy and longevity of the modding community. | Only applicable to games with Workshop support. |

### **4.3. Concluding Recommendations**

Based on this comprehensive analysis, the following strategic actions are recommended for the development of a system to programmatically assess product viability on Steam:

* **Prioritize Core Data Pipelines:** The initial engineering effort should be focused on implementing robust, scalable data ingestion pipelines for the two most potent sources of signals: the ISteamNews/GetNewsForApp endpoint and the store.steampowered.com/appreviews/ endpoint. These two sources provide the most direct and universally applicable data for all games on the platform.  
* **Invest in Advanced Text Sanitization:** The unstructured and non-standard format of the contents field in the ISteamNews response is a significant technical challenge. A substantial investment in building or integrating a high-fidelity text sanitization library capable of handling Steam's proprietary BBCode is not optional; it is a prerequisite for high-quality semantic analysis.  
* **Design for Temporal Correlation:** The data model and database schema should be explicitly designed to support temporal analysis. The ability to join data across different endpoints based on timestamps—for example, linking a patch release date from ISteamNews to a subsequent wave of review updates from appreviews—is where the most powerful and nuanced analytical models will emerge.  
* **Integrate Workshop Data as a Differentiator:** For games that support it, integrating data from the IPublishedFileService to analyze Workshop activity will provide a significant competitive advantage. This signal is a powerful indicator of long-term product health and community investment that is often overlooked by more superficial analyses.  
* **Adopt a Continuous Research Methodology:** The Steam API ecosystem is not static, and its official documentation is demonstrably incomplete. It is essential to adopt a continuous research and discovery process that includes the programmatic re-evaluation of API capabilities (e.g., via GetSupportedAPIList) and the active monitoring of developer community forums for newly discovered features or undocumented parameters. This proactive stance will ensure the analysis model remains effective and leverages the best available data over the long term.

#### **Works cited**

1. Steam Web API Documentation, accessed September 21, 2025, [https://steamcommunity.com/dev](https://steamcommunity.com/dev)  
2. Steamworks Web API Reference, accessed September 21, 2025, [https://partner.steamgames.com/doc/webapi](https://partner.steamgames.com/doc/webapi)  
3. How to get localized game news from Steam Web API? \- Stack Overflow, accessed September 21, 2025, [https://stackoverflow.com/questions/72189952/how-to-get-localized-game-news-from-steam-web-api](https://stackoverflow.com/questions/72189952/how-to-get-localized-game-news-from-steam-web-api)  
4. How do you get a Steam News feed? : r/gamemaker \- Reddit, accessed September 21, 2025, [https://www.reddit.com/r/gamemaker/comments/anau12/how\_do\_you\_get\_a\_steam\_news\_feed/](https://www.reddit.com/r/gamemaker/comments/anau12/how_do_you_get_a_steam_news_feed/)  
5. Steam Web API \- Valve Developer Community, accessed September 21, 2025, [https://developer.valvesoftware.com/wiki/Steam\_Web\_API](https://developer.valvesoftware.com/wiki/Steam_Web_API)  
6. Steam Web API — python-valve 0.2.0 documentation, accessed September 21, 2025, [https://python-valve.readthedocs.io/en/latest/api.html](https://python-valve.readthedocs.io/en/latest/api.html)  
7. ISteamNews Interface (Steamworks Documentation), accessed September 21, 2025, [https://partner.steamgames.com/doc/webapi/isteamnews](https://partner.steamgames.com/doc/webapi/isteamnews)  
8. Expand GetNewsForApp API endpoint :: Suggestions / Ideas \- Steam Community, accessed September 21, 2025, [https://steamcommunity.com/discussions/forum/10/4202490864583058613/](https://steamcommunity.com/discussions/forum/10/4202490864583058613/)  
9. User Reviews in Steam API :: Help and Tips, accessed September 21, 2025, [https://steamcommunity.com/discussions/forum/1/1636418037466190064/](https://steamcommunity.com/discussions/forum/1/1636418037466190064/)  
10. Steam API: How to get the recent review sentiment? :: Help and Tips, accessed September 21, 2025, [https://steamcommunity.com/discussions/forum/1/4030224579607523412/](https://steamcommunity.com/discussions/forum/1/4030224579607523412/)  
11. Is there a way to get more information for a Steam game through API like ratings or tags?, accessed September 21, 2025, [https://stackoverflow.com/questions/49810662/is-there-a-way-to-get-more-information-for-a-steam-game-through-api-like-ratings](https://stackoverflow.com/questions/49810662/is-there-a-way-to-get-more-information-for-a-steam-game-through-api-like-ratings)  
12. User Reviews \- Get List (Steamworks Documentation), accessed September 21, 2025, [https://partner.steamgames.com/doc/store/getreviews](https://partner.steamgames.com/doc/store/getreviews)  
13. Efficiently Scraping Steam Game Reviews with Python: A Comprehensive Guide \- Medium, accessed September 21, 2025, [https://medium.com/codex/efficiently-scraping-steam-game-reviews-with-python-a-comprehensive-guide-3a5732cb7f0b](https://medium.com/codex/efficiently-scraping-steam-game-reviews-with-python-a-comprehensive-guide-3a5732cb7f0b)  
14. User Reviews (Steamworks Documentation), accessed September 21, 2025, [https://partner.steamgames.com/doc/store/reviews](https://partner.steamgames.com/doc/store/reviews)  
15. Developer Responses to Steam Reviews (Discussion) : r/ItsAllAboutGames \- Reddit, accessed September 21, 2025, [https://www.reddit.com/r/ItsAllAboutGames/comments/1insng5/developer\_responses\_to\_steam\_reviews\_discussion/](https://www.reddit.com/r/ItsAllAboutGames/comments/1insng5/developer_responses_to_steam_reviews_discussion/)  
16. Steam Workshop (Steamworks Documentation), accessed September 21, 2025, [https://partner.steamgames.com/doc/features/workshop](https://partner.steamgames.com/doc/features/workshop)  
17. TSteamUGC \- BlitzMax, accessed September 21, 2025, [https://blitzmax.org/docs/en/api/steam/steam.steamsdk/tsteamugc/](https://blitzmax.org/docs/en/api/steam/steam.steamsdk/tsteamugc/)  
18. Steam Workshop Implementation Guide (Steamworks Documentation), accessed September 21, 2025, [https://partner.steamgames.com/doc/features/workshop/implementation](https://partner.steamgames.com/doc/features/workshop/implementation)  
19. Steamworks API Reference, accessed September 21, 2025, [https://partner.steamgames.com/doc/api](https://partner.steamgames.com/doc/api)  
20. IPublishedFileService Interface \- Sign in to Steamworks, accessed September 21, 2025, [https://partner.steamgames.com/doc/webapi/ipublishedfileservice](https://partner.steamgames.com/doc/webapi/ipublishedfileservice)  
21. ISteamRemoteStorage Interface (Steamworks Documentation), accessed September 21, 2025, [https://partner.steamgames.com/doc/webapi/isteamremotestorage?l=arabic](https://partner.steamgames.com/doc/webapi/isteamremotestorage?l=arabic)  
22. ISteamRemoteStorage Interface \- Sign in to Steamworks, accessed September 21, 2025, [https://partner.steamgames.com/doc/webapi/isteamremotestorage](https://partner.steamgames.com/doc/webapi/isteamremotestorage)  
23. Web API for workshop items? : r/Steam \- Reddit, accessed September 21, 2025, [https://www.reddit.com/r/Steam/comments/30l5au/web\_api\_for\_workshop\_items/](https://www.reddit.com/r/Steam/comments/30l5au/web_api_for_workshop_items/)  
24. Web API Overview \- Sign in to Steamworks, accessed September 21, 2025, [https://partner.steamgames.com/doc/webapi\_overview](https://partner.steamgames.com/doc/webapi_overview)  
25. Steamworks API Overview, accessed September 21, 2025, [https://partner.steamgames.com/doc/sdk/api](https://partner.steamgames.com/doc/sdk/api)