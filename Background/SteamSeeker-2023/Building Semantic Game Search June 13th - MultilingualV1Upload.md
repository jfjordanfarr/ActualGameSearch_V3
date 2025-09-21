### June 13th - MultilingualV1Upload.ipynb

#### Imports

```python
# Imports
import h5py
import numpy as np
import pandas as pd
import csv
import json
import os
import re
import tiktoken
import openai
import time
import random
import math
import pickle
from datetime import datetime
from scipy.stats.mstats import gmean
```

#### Constants

```python
db_generation_folder = '../../DB-Generation/'
review_embeddings_output_folder = os.path.join(db_generation_folder, 'Processed/Reviews/Embeddings/')
```

```python
# Constants
GAME_COLLECTION_NAME = 'steam_metatadata_db'  # Collection name
REVIEW_COLLECTION_NAME = 'steam_review_db'  # Collection name
DIMENSION = 1536  # Embeddings size
COUNT = 50000  # Max titles to embed and insert.
URI = os.environ.get("ZILLIZ_URI")  # Endpoint URI obtained from Zilliz Cloud
USER = os.environ.get("ZILLIZ_USERNAME")  # Username specified when you created this database
PASSWORD = os.environ.get("ZILLIZ_PASS")  # Password set for that account
OPENAI_ENGINE = 'text-embedding-ada-002'  # Which engine to use
openai.api_key = os.environ.get("OPENAI_API_KEY")  
```

#### Raw Data Import

```python
# Data preprocessing
metadata_census = pd.read_feather(os.path.join(db_generation_folder, 'Metadata_Census_May31_2023.feather'))
```

```python
review_census = pd.read_feather(os.path.join(db_generation_folder, 'Review_Census_May31_2023.feather'))
```

```python
review_cutoff_date = review_census['datetime_timestamp_updated'].max()
```

#### Methods

##### Preprocess Text
```python
#Method for cleaning up review and store page texts
def preprocess_text(text):
    if not isinstance(text, str):
        return ''  # Return an empty string if the input is not a string
    text = re.sub(r'<[^<]+?>', ' ', text)  # Remove formatting tags
    text = re.sub(r'\[.*?\]', " ", text)   # Remove block tags
    text = re.sub(r'http\S+', " ", text)   # Remove web links
    #Commented out one processing step which is prone to work poorly with multilingual inputs.
    #text = re.sub(r'^[^a-zA-Z]+', " ", text)  # Remove non-alphabetical characters at the beginning of a string (for dashed lists)
    text = re.sub(r'quot;', "", text)  # Remove front quotes
    text = re.sub(r'&quot', "", text)  # Remove back quotes
    text = re.sub(r'\n', " ", text)    # Remove newlines
    text = re.sub(r'\-\-+', ' ', text)  # Remove extra dashes (2+ dashes become a single space, single dashes are kept in)
    text = re.sub(r'\.\.+', '. ', text)  # Remove extra periods (2+ periods become a period with a single space, single periods are kept in)
    text = re.sub(r'\\r', ' ', text)    # Remove \r replies
    text = re.sub(r'(((.)\3+)\3+)\3+', r'\3', text)  # Remove extra characters (4+ of the same letter become a single letter, single letters are kept in)
    text = re.sub(r'\\', ' ', text)     # Remove any remaining backslashes
    text = re.sub(r'\s+', ' ', text)    # Remove extra whitespace, tab delimiters, etc.
    text = re.sub(r' +', ' ', text)     # Remove extra spaces
    return text
```

##### Embedding Methods
```python
# Get the encoding for a specific model
encoding = tiktoken.encoding_for_model("gpt-3.5-turbo")

def count_tokens(text):
    # Encode the text into tokens
    tokens = encoding.encode(text)
    return len(tokens)

def shorten_text_to_max_tokens(text, token_limit=8000):
    # Encode the text into tokens
    tokens = encoding.encode(text)
    # Shorten the text to the maximum number of tokens
    return encoding.decode(tokens[:token_limit])

# Extract embedding from text using OpenAI
def embed(text, max_attempts=5):
    for attempt in range(max_attempts):
        try:
            return openai.Embedding.create(
                input=text, 
                engine=OPENAI_ENGINE)["data"][0]["embedding"]
        except Exception as e:
            print(f"Attempt {attempt+1} failed with error: {e}")
            time.sleep(2)  # Sleep for 2 seconds before retrying
    raise Exception(f"Failed to get embedding after {max_attempts} attempts")
```

##### Get Bulk Review Stats
```python
def get_bulk_review_stats(processed_reviews):

    # Simple properties we collect.
    positivity_rating = len(processed_reviews[processed_reviews['voted_up'] == True]) / len(processed_reviews)

    # The mathier bulk properties we collect.
    geometric_mean_word_count = gmean(processed_reviews['word_count'].clip(lower=1))
    geometric_mean_unique_word_count = gmean(processed_reviews['unique_word_count'].clip(lower=1))
    geometric_mean_resonance_score = gmean(processed_reviews['resonance_score'].clip(lower=1))
    geometric_mean_hours_played = gmean(processed_reviews['author.playtime_forever'].clip(lower=1))
    geometric_mean_num_games_owned = gmean(processed_reviews['author.num_games_owned'].clip(lower=1))
    geometric_mean_author_num_reviews = gmean(processed_reviews['author.num_reviews'].clip(lower=1))
    
    #Convert Linux timestamps for review creation into datetime objects, 
    #then find the date of the oldest review
    first_review_date = processed_reviews.loc[:, 'datetime_timestamp_created'].min().date()
    last_review_date = processed_reviews.loc[:, 'datetime_timestamp_created'].max().date()
    inferred_release_year = first_review_date.year

    return {
        'positivity_rating': positivity_rating,
        'geometric_mean_word_count': geometric_mean_word_count,
        'geometric_mean_unique_word_count': geometric_mean_unique_word_count,
        'geometric_mean_resonance_score': geometric_mean_resonance_score,
        'geometric_mean_hours_played': geometric_mean_hours_played,
        'geometric_mean_num_games_owned': geometric_mean_num_games_owned,
        'geometric_mean_author_num_reviews': geometric_mean_author_num_reviews,
        'first_review_date': first_review_date.strftime('%Y-%m-%d'),
        'last_review_date': last_review_date.strftime('%Y-%m-%d'),
        'inferred_release_year': int(inferred_release_year)
    }
```

##### Categories
```python
# Find all category int values for a given game. Expects a row of the app_metadata dataframe.
def find_categories(row):
    categories = re.findall(r'"id": \d+', row['categories'])
    categories = [int(re.findall(r'\d+', category)[0]) for category in categories]
    return categories

#Example usage
#find_categories(metadata_census.iloc[0])
```

```python
# Define a function which extracts the 'category' tags from a given metadata row, 
# then returns a dictionary of English category names coupled to True or False values for each category available in the tag table.
def category_tags_to_bools(metadata_row, category_tag_table):
    tags_in_row = find_categories(metadata_row)
    #return tags_in_row

    # Create a dictionary of category names to boolean values
    return_categories = category_tag_table.copy()
    return_categories['has_tag'] = False

    # Iterate through the tags in the row, and set the corresponding boolean value to True
    for tag in tags_in_row:
        return_categories.loc[return_categories['Category ID'] == tag, 'has_tag'] = True

    # Return the dictionary of category names to boolean values
    category_bools = return_categories.set_index('Category Name')['has_tag'].to_dict()
    
    return category_bools
    
#Example usage
#category_tags_to_bools(metadata_census.iloc[0], known_category_enums)
```

#### Filtering Metadata and Reviews to find Games That can be Uploaded

```python
review_census['appid'].unique()
```
```
array([307110,     10,    100, ..., 999960, 999980, 999990], dtype=int64)
```

```python
# Find all appids that are actually released games. Try to get reviews for THOSE games.
orig_app_count = len(metadata_census)
filtered_metadata = metadata_census.copy()
# Convert the price columns to float and divide by 100
filtered_metadata['price_overview.initial'] = filtered_metadata['price_overview.initial'].astype(float) / 100
filtered_metadata['price_overview.final'] = filtered_metadata['price_overview.final'].astype(float) / 100
filtered_metadata['appid'] = filtered_metadata['steam_appid'].astype(int)

print("Original metadata length: ", len(filtered_metadata))
filtered_metadata = filtered_metadata.drop_duplicates(subset=['steam_appid'])
print("length of filtered_metadata after duplicate pass: ", len(filtered_metadata))
filtered_metadata = filtered_metadata[filtered_metadata['type'] == 'game']
print("length of filtered_metadata after type pass: ", len(filtered_metadata))
#Remove 18+ games. Remove any '+' from the age rating and convert to float to check it.
filtered_metadata = filtered_metadata[filtered_metadata['required_age'].apply(lambda x: re.sub(r'\+', '', x)).astype(float) < 18]
print("length of filtered_metadata after age pass: ", len(filtered_metadata))
#Remove any games which have not released
filtered_metadata = filtered_metadata[filtered_metadata['release_date.coming_soon'] == False]
print("length of filtered_metadata after release pass: ", len(filtered_metadata))
#Remove any games with empty, NA, or NaN short descriptions
filtered_metadata = filtered_metadata[filtered_metadata['short_description'].notna()]
print("length of filtered_metadata after short description pass: ", len(filtered_metadata))
#Remove any games with empty, NA, or NaN detailed descriptions
filtered_metadata = filtered_metadata[filtered_metadata['detailed_description'].notna()]
print("length of filtered_metadata after detailed description pass: ", len(filtered_metadata))

# Skipping naughty words filter pass so that we find all games that have reviews, even if they have naughty words in their descriptions.
filter_words = ['hentai']
filtered_metadata = filtered_metadata[~filtered_metadata['name'].str.contains(''.join(filter_words), case=False)]
filtered_metadata = filtered_metadata[~filtered_metadata['short_description'].str.contains(''.join(filter_words), case=False)]
filtered_metadata = filtered_metadata[~filtered_metadata['detailed_description'].str.contains(''.join(filter_words), case=False)]
print("length of filtered_metadata after filter words pass: ", len(filtered_metadata))

appids_with_any_reviews = review_census['appid'].unique()
filtered_metadata = filtered_metadata[(filtered_metadata['steam_appid'].astype(int).isin(appids_with_any_reviews))]
print("length of filtered_metadata after reviews pass: ", len(filtered_metadata))

print("Final length of filtered_metadata: ", len(filtered_metadata))
print("Number of games removed: ", orig_app_count - len(filtered_metadata))
```
```
Original metadata length:  150490
length of filtered_metadata after duplicate pass:  150334
length of filtered_metadata after type pass:  89130
length of filtered_metadata after age pass:  88789
length of filtered_metadata after release pass:  72696
length of filtered_metadata after short description pass:  72696
length of filtered_metadata after detailed description pass:  72696
length of filtered_metadata after filter words pass:  72070
length of filtered_metadata after reviews pass:  57491
Final length of filtered_metadata:  57491
Number of games removed:  92999

```

```python
# Generate a filtered_reviews dataframe which only contains reviews for games that are in the filtered_metadata dataframe
filtered_reviews = review_census[review_census['appid'].astype(int).isin(filtered_metadata['steam_appid'].astype(int))]
print("length of filtered_reviews: ", len(filtered_reviews))
```
```
length of filtered_reviews:  51099264
```

```python
# Drop duplicate texts from filtered_reviews
filtered_reviews = filtered_reviews.drop_duplicates(subset=['processed_review'])
```

```python
filtered_reviews.keys()
```
```
Index(['recommendationid', 'language', 'voted_up', 'weighted_vote_score',
       'received_for_free', 'author.num_games_owned', 'author.num_reviews',
       'author.playtime_forever', 'author.playtime_at_review',
       'processed_review', 'word_count', 'unique_word_count',
       'resonance_score', 'datetime_timestamp_created',
       'datetime_timestamp_updated', 'steam_release_year', 'appid'],
      dtype='object')
```

```python
# Remove all reviews with fewer than 20 unique words
filtered_reviews = filtered_reviews[filtered_reviews['unique_word_count'] >= 20]
print("length of filtered_reviews after unique word count pass: ", len(filtered_reviews))

# Remove all reviews which were gifted
filtered_reviews = filtered_reviews[filtered_reviews['received_for_free'] == False]
print("length of filtered_reviews after received for free pass: ", len(filtered_reviews))
```
```
length of filtered_reviews after unique word count pass:  19319188
length of filtered_reviews after received for free pass:  19170666
```

```python
# Count the number of reviews per appid in the filtered_reviews table.
# This will be used to filter out games with too few reviews.

# Create a dataframe of appid and review count
review_counts = filtered_reviews.groupby('appid').count()['processed_review'].reset_index()
review_counts
```
```
         appid  processed_review
0           10             12720
1           20              1434
2           30               959
3           40               550
4           50              4693
...        ...               ...
55097  2425310                 2
55098  2425400                 3
55099  2426330                 1
55100  2427360                 4
55101  2432780                 1

[55102 rows x 2 columns]
```

```python
# Filter out games with fewer than 20 qualifying reviews
filtered_reviews = filtered_reviews[filtered_reviews['appid'].isin(review_counts[review_counts['processed_review'] >= 20]['appid'])]
filtered_reviews.reset_index(inplace=True, drop=True)
print("length of filtered_reviews after review count pass: ", len(filtered_reviews))
```
```
length of filtered_reviews after review count pass:  18971669
```

```python
# Add time weighted resonance columns
filtered_reviews['days_since_review_posted'] = (pd.to_datetime('2023-06-01') - filtered_reviews['datetime_timestamp_updated']).dt.days
filtered_reviews['log365_days_since_review'] = filtered_reviews['days_since_review_posted'].apply(lambda x: math.log(x, 365)).clip(lower=1)
filtered_reviews['time_weighted_resonance'] = filtered_reviews['resonance_score'] / filtered_reviews['log365_days_since_review']
```

```python
filtered_reviews
```
```
          recommendationid language  voted_up  weighted_vote_score  \
0                138169488  english      True             0.476190   
1                137998280  english      True             0.000000   
2                137324445  english     False             0.551387   
3                136922264   french      True             0.000000   
4                136540892   german      True             0.000000   
...                    ...      ...       ...                  ...   
18971664          69039355  english     False             0.527994   
18971665          68966670  english      True             0.508470   
18971666          68951669  english      True             0.476190   
18971667          68894944  english      True             0.523810   
18971668          68816417  english      True             0.000000   

          received_for_free  author.num_games_owned  author.num_reviews  \
0                     False                       0                   8   
1                     False                       0                  16   
2                     False                     420                  99   
3                     False                     117                  16   
4                     False                       0                   2   
...                     ...                     ...                 ...   
18971664              False                       0                  14   
18971665              False                     167                   3   
18971666              False                       0                   2   
18971667              False                       0                   2   
18971668              False                     421                  15   

          author.playtime_forever  author.playtime_at_review  \
0                          1363.0                     1358.0   
1                           571.0                      571.0   
2                           145.0                      145.0   
3                          3689.0                     3361.0   
4                          2233.0                     2233.0   
...                           ...                        ...   
18971664                   2675.0                      805.0   
18971665                   2155.0                     1155.0   
18971666                    386.0                      193.0   
18971667                    706.0                      347.0   
18971668                    358.0                      179.0   

                                           processed_review  word_count  \
0         Not a game for everyone, but if you can get pa...          37   
1         its ok if you have a bunch of friends to play ...          25   
2         You die, you reopen the lobby you re-set the r...          61   
3         Jeu exceptionnel, seul bémol c'est pour trouve...          58   
4         Dieses Spiel ist nur geeignet, wenn man ein di...          36   
...                                                     ...         ...   
18971664  Discord groups have ruined this game. People a...          94   
18971665  This game is absolutely incredible, the polish...          41   
18971666  This game is actually a lot of fun. there are ...          52   
18971667  One of the best social deception games out the...          59   
18971668  This is a genuinely fun game! I'm glad I picke...          50   

          unique_word_count  resonance_score datetime_timestamp_created  \
0                        32       491.899417        2023-05-12 01:00:12   
1                        22       201.250704        2023-05-08 09:20:28   
2                        50       168.555139        2023-04-26 10:25:07   
3                        48       387.087664        2023-04-18 15:44:56   
4                        31       330.192725        2023-04-11 15:54:02   
...                     ...              ...                        ...   
18971664                 74       694.753794        2020-05-12 20:53:10   
18971665                 31       520.153185        2020-05-11 13:23:06   
18971666                 44       292.102350        2020-05-11 07:40:14   
18971667                 50       470.346406        2020-05-10 06:17:15   
18971668                 39       185.742813        2020-05-08 19:05:06   

         datetime_timestamp_updated  steam_release_year   appid  \
0               2023-05-12 01:00:12                2017  307110   
1               2023-05-08 09:20:28                2017  307110   
2               2023-04-26 10:25:07                2017  307110   
3               2023-04-18 15:44:56                2017  307110   
4               2023-04-11 15:54:02                2017  307110   
...                             ...                 ...     ...   
18971664        2020-05-12 20:53:10                2020  999860   
18971665        2020-05-11 13:23:06                2020  999860   
18971666        2020-05-11 07:40:14                2020  999860   
18971667        2020-05-10 06:17:15                2020  999860   
18971668        2020-05-08 19:05:06                2020  999860   

          days_since_review_posted  log365_days_since_review  \
0                               19                  1.000000   
1                               23                  1.000000   
2                               35                  1.000000   
3                               43                  1.000000   
4                               50                  1.000000   
...                            ...                       ...   
18971664                      1114                  1.189124   
18971665                      1115                  1.189277   
18971666                      1115                  1.189277   
18971667                      1116                  1.189429   
18971668                      1118                  1.189732   

          time_weighted_resonance  
0                      491.899417  
1                      201.250704  
2                      168.555139  
3                      387.087664  
4                      330.192725  
...                           ...  
18971664               584.256569  
18971665               437.369404  
18971666               245.613474  
18971667               395.438986  
18971668               156.121558  

[18971669 rows x 20 columns]
```

#### Generate Embeddings for Filtered Reviews

```python
# ChatGPT-generated code which has a placeholder embedding function
# This is written to generate embeddings for the Steam review dataset
# in a format that is precisely compatible with Milvus (int64 id, float32 vector)


total_reviews_in_final_dataset = 0
curr_appid = 0
cur_index = 0
num_appids = len(filtered_reviews['appid'].unique())

for appid in filtered_reviews['appid'].unique():
    curr_appid = appid

    # Filter dataframe for current appid
    appid_df = filtered_reviews[filtered_reviews['appid'] == appid].sort_values(by='time_weighted_resonance', ascending=False)[:200] #Take up to top 200 most resonant reviews.

    print("Number of reviews for appid {}: {}".format(appid, len(appid_df)))
    total_reviews_in_final_dataset += len(appid_df)

    # Initialize a list to store the embeddings
    embeddings = []

    for _, row in appid_df.iterrows():
        # Generate the embedding for the current review
        review_embedding = embed(shorten_text_to_max_tokens(row['processed_review'], 8100))

        # Add the recommendationid and the embedding to the list
        embeddings.append((row['recommendationid'], review_embedding))

    # Convert the list of tuples to a structured numpy array
    embeddings_array = np.array(embeddings, dtype=[('recommendationid', 'i8'), ('embedding', 'f4', (1536,))])
    

    # Save the structured array to a pickle file
    filename = os.path.join(review_embeddings_output_folder, str(appid) + '.pickle')
    with open(filename, 'wb') as f:
        pickle.dump(embeddings_array, f)

    if cur_index % 100 == 0:
        print("Completed {} of {} appids".format(cur_index, num_appids))
        print("Total reviews in dataset so far: {}".format(total_reviews_in_final_dataset))

    cur_index += 1

```
```
...
Number of reviews for appid 994870: 20
Number of reviews for appid 995050: 98
Number of reviews for appid 995070: 92
Number of reviews for appid 995230: 200
Number of reviews for appid 995240: 35
Number of reviews for appid 995450: 30
Number of reviews for appid 995460: 200
Number of reviews for appid 995470: 54
Number of reviews for appid 995660: 23
Number of reviews for appid 995980: 200
Number of reviews for appid 996080: 49
Number of reviews for appid 996380: 200
Number of reviews for appid 996580: 200
Attempt 1 failed with error: The server had an error while processing your request. Sorry about that!
Attempt 1 failed with error: The server had an error while processing your request. Sorry about that!
Number of reviews for appid 996770: 200
Attempt 1 failed with error: The server had an error while processing your request. Sorry about that!
Number of reviews for appid 99700: 167
Number of reviews for appid 997010: 200
Number of reviews for appid 997070: 200
Number of reviews for appid 997380: 200
Number of reviews for appid 997700: 107
Number of reviews for appid 997720: 41
Number of reviews for appid 9980: 35
Number of reviews for appid 998220: 101
Number of reviews for appid 998740: 200
Number of reviews for appid 998790: 25
Number of reviews for appid 998930: 200
Number of reviews for appid 998990: 30
Number of reviews for appid 99900: 68
Number of reviews for appid 999020: 200
Number of reviews for appid 999190: 20
Number of reviews for appid 999220: 200
Number of reviews for appid 999350: 51
Number of reviews for appid 999410: 30
Number of reviews for appid 999640: 25
Number of reviews for appid 999660: 108
Number of reviews for appid 999860: 32
```