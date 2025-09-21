 Steam Seeker Node Js Project

## APIs

#### Game.ts

```typescript
import type { NextApiRequest, NextApiResponse } from "next";
import { DataType, MilvusClient } from "@zilliz/milvus2-sdk-node/dist/milvus";
import { Configuration, OpenAIApi } from "openai";

const client = new MilvusClient({
  address: process.env.ZILLIZ_HOST || "",
  username: process.env.ZILLIZ_USERNAME || "",
  password: process.env.ZILLIZ_PASSWORD || "",
  ssl: true,
});
const openAI = new OpenAIApi(
  new Configuration({
    apiKey: process.env.OPENAI_API_KEY,
  })
);

export default async function handler(
  req: NextApiRequest,
  res: NextApiResponse<
    | { game: Record<string, any>; reviews: Record<string, any>[] }
    | { error: any }
  >
) {
  if (req.method === "POST" && req.body.appid) {
    try {
      const game = (
        await client.query({
          collection_name: "ml_games",
          expr: `appid in [${req.body.appid}]`,
          output_fields: ["*"],
        })
      ).data[0];

      const reviews = (
        await client.query({
          collection_name: "ml_reviews",
          expr: `appid in [${req.body.appid}]`,
          output_fields: ["*"],
        })
      ).data;

      res.status(200).json({
        game,
        reviews,
      });
    } catch (error) {
      console.error(error);
      res.status(500).json({ error });
      return;
    }
  } else {
    res.status(405).json({
      error: "Must use POST request with `game` JSON body param.",
    });
  }
}

```

#### Recommendation.ts

```python
import type { NextApiRequest, NextApiResponse } from "next";
import { DataType, MilvusClient } from "@zilliz/milvus2-sdk-node/dist/milvus";
import { Configuration, OpenAIApi } from "openai";

// Global Paramters
const OPTIMAL_RESULT_DISTANCE = 0.19;              // Euclidean (L2) distance cutoff for search results of any type. Results with 'score' <= 0.2 have been found to be optimal for balancing accuracy and ranking.
const RESULT_DISTANCE_THRESHOLD_INCREMENT = 0.01;  // How much to increase the distance cutoff by if there are fewer than MIN_RESULTS_AFTER_DISTANCE_CUTOFF results with optimal 'score'
const MAX_RESULT_DISTANCE = 0.3;                   // Maximum L2 distance cutoff for search results of any type. Final fallback value. If there isn't anything within this distance, the search will fail.
const MIN_GAME_RESULTS_AFTER_DISTANCE_CUTOFF = 20; // Minimum number of GAMES to return after applying the distance cutoffs above. If there are fewer than this number of results, the distance cutoffs will be relaxed until this number is met. 
const NUM_REVIEW_SEARCH_RESULTS_BROAD = 2000;      // top_k for review-level search
const NUM_REVIEW_SEARCH_RESULTS_NARROW = 200;      // top_k for review-level search
const NUM_RANKED_GAMES_BROAD = 50;                 // Number of actual entries to show to the user when broad search is enabled.
const NUM_RANKED_GAMES_NARROW = 50;                // Number of actual entries to show to the user when narrow search is enabled.
const MIN_REVIEW_HITS_TO_CONSIDER_APPID = 1;       // Because games have up to 200 reviews representing them, we should expect several reviews to come up per game in a regular search.
const PROXIMITY_SCORE_CONTRIBUTION = 2;            // Default 1. At 1, a proximity score of 0.2 is equal in value to a resonance score of 555.
const NEGATIVE_REVIEW_WEIGHT = 0.5                 // Default 0.5. This is the multiplier applied to the resonance score of negative reviews. 

type Review = {
  id: string;
  score: number;
  appid: string;
  recommended: boolean;
  language: string;
  review: string;
  playtime_forever: number;
  review_date: string;
  resonance_score: number;
  time_weighted_resonance_score: number;
  weighted_score?: number;
};

const client = new MilvusClient({
  address: process.env.ZILLIZ_HOST || "",
  username: process.env.ZILLIZ_USERNAME || "",
  password: process.env.ZILLIZ_PASSWORD || "",
  ssl: true,
});

const openAI = new OpenAIApi(
  new Configuration({
    apiKey: process.env.OPENAI_API_KEY,
  })
);

export default async function handler(
  req: NextApiRequest,
  res: NextApiResponse<{ games: Record<string, any>[] } | { error: any }>
) {
  if (req.method === "POST" && req.body.query) {
    try {
      const queryEmbeddingResponse = (
        await openAI.createEmbedding({
          model: "text-embedding-ada-002",
          input: req.body.query,
        })
      ).data.data[0];

      
      const reviewSearchResponse = await client.search({
        collection_name: "ml_reviews",
        output_fields: [
          "review_id",
          "appid",
          "recommended",
          "language",
          "review",
          "playtime_forever",
          "review_date",
          "resonance_score",
          "time_weighted_resonance_score"
        ],
        search_params: {
          anns_field: "embedding",
          topk: NUM_REVIEW_SEARCH_RESULTS_BROAD.toString(),
          metric_type: "L2",
          params: JSON.stringify({}),
        },
        vectors: [queryEmbeddingResponse.embedding],
        vector_type: DataType.FloatVector,
      });

      const reviews = reviewSearchResponse.results as Review[];

      // Count the number of reviews per appid
      const appidToReviewCount: Record<string, number> = {};
      reviews.forEach((review) => {
        if (!appidToReviewCount[review.appid]) {
          appidToReviewCount[review.appid] = 1;
        } else {
          appidToReviewCount[review.appid]++;
        }
      });

      // Filter out appids with too few reviews
      const filteredReviews = reviews.filter(
        (review) => appidToReviewCount[review.appid] >= MIN_REVIEW_HITS_TO_CONSIDER_APPID
      );

      const appidToWeightedScore: Record<string, number> = {};
      filteredReviews.forEach((review) => {
        let recommendedToFloat = review.recommended ? 1.0 : 0.5;
        review.score = Math.min(review.score, 0.15) //Clamping score before dividing by it.
        let proximityComponent = (1 / (0.225 * review.score * review.score * review.score )) * PROXIMITY_SCORE_CONTRIBUTION; // Essentially 
        let resonanceComponent = review.time_weighted_resonance_score * recommendedToFloat;

        //Logarithmize to recognize the diminishing returns of each component
        //proximityComponent = Math.log(proximityComponent + 1);
        //resonanceComponent = Math.log(resonanceComponent + 1);

        //NOTE: It appears that detecting gems involves MULTIPLYING proximity and resonance. However, this severely limits the variety of search results.
        review.weighted_score = proximityComponent + resonanceComponent;

        if (!appidToWeightedScore[review.appid]) {
          appidToWeightedScore[review.appid] = review.weighted_score;
        } else {
          appidToWeightedScore[review.appid] += review.weighted_score;
        }
      });

      // Sort appidToWeightedScore from highest to lowest value
      const appidToWeightedScoreSorted = Object.fromEntries(
        Object.entries(appidToWeightedScore).sort(([, a], [, b]) => b - a)
      );

      const gameObjects = (
        await client.query({
          collection_name: "ml_games",
          expr: `appid in [${Object.keys(appidToWeightedScoreSorted).join(",")}]`,
          output_fields: ["*"],
        })
      ).data;

      const rankedGames = Object.keys(appidToWeightedScoreSorted)
        .map((appId) => ({
          ...gameObjects.find((game) => game.appid === appId),
          appId,
          score: appidToWeightedScoreSorted[appId]//.scores.reduce((acc: number, score: number) => acc + score, 0) / groupedGamesByReview[appId].scores.length,
        }))
        .sort((a, b) => b.score - a.score)
        .slice(0, NUM_RANKED_GAMES_BROAD);

      res.status(200).json({
        games: rankedGames,
      });

    } catch (error) {
      console.error(error);
      res.status(500).json({ error });
      return;
    }
  }
}

```