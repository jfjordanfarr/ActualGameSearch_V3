### June 14th-18th - MultilingualUploadV1ZILLIZ.ipnynb

>[!Info]
>This is the second part to MultilingualV1Upload.ipynb. In that notebook, we created the embeddings for up to 200 reviews per game. Here, we will dictate the schema, generate the objects in memory, and upload them to Zilliz

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
from pymilvus import connections, FieldSchema, CollectionSchema, DataType, Collection, utility
import seaborn as sns
import matplotlib.pyplot as plt
```

#### Constants

```python
db_generation_folder = '../../DB-Generation/'
review_embeddings_output_folder = os.path.join(db_generation_folder, 'Processed/Reviews/Embeddings/')
game_embeddings_output_folder = os.path.join(db_generation_folder, 'Processed/Games/Embeddings/')
metadata_embeddings_output_folder = os.path.join(db_generation_folder, 'Processed/Games/Embeddings_Metadata_Only/')
```

```python
# Constants
GAME_COLLECTION_NAME = 'ml_games'  # Collection name
REVIEW_COLLECTION_NAME = 'ml_reviews'  # Collection name
DIMENSION = 1536  # Embeddings size
ZILLIZ_FREE_URI = os.environ.get("ZILLIZ_FREE_URI")  # Public endpoint for free tier instance. Make sure port 443 is NOT included.
ZILLIZ_PAID_URI = os.environ.get("ZILLIZ_PAID_URI")  # Public endpoint for paid instance. Make sure port 443 is included.
USER = os.environ.get("ZILLIZ_USERNAME")  # Username specified when you created this database
PASSWORD = os.environ.get("ZILLIZ_PASS")  # Password set for that account
ZILLIZ_API_KEY = os.environ.get("ZILLIZ_API_KEY") # API Key for Zilliz free tier instance
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
category_tag_table = pd.read_csv(os.path.join(db_generation_folder, 'UsedSteamTags.csv'))
```

#### Common Methods

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

##### Embeddings

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

##### Categories

```python
# Find all category int values for a given game. Expects a row of the app_metadata dataframe.
def find_categories(row):
    #categories = str(row['categories'])
    #categories = re.findall(r'"id": \d+', categories)
    #categories = [int(re.findall(r'\d+', category)[0]) for category in categories]
    #return categories

    # This changed after the metadata had to come in and out of feather file format!
    # Because of the way it saved, the JSON strings tucked inside the categories column came back out as an array of dicts.
    # No string magic required.
    categories = row['categories']
    if categories is None:
        return []

    return [id_dict['id'] for id_dict in row['categories']]

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
    
# Example usage
#category_tags_to_bools(app_metadata.iloc[0], known_category_enums)
```

#### Common Filtering

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
#Remove games which are unlisted. These are entries where column 'is_free' is False, but pd.isna('price_overview.final') is True.
filtered_metadata = filtered_metadata[~((filtered_metadata['is_free'] == False) & (filtered_metadata['price_overview.final'].isna()))]
print("length of filtered_metadata after unlisted pass: ", len(filtered_metadata))

# Filter out games which claim not to be 18+ but are absolutely still 18+.
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
length of filtered_metadata after unlisted pass:  69254
length of filtered_metadata after filter words pass:  68629
length of filtered_metadata after reviews pass:  56778
Final length of filtered_metadata:  56778
Number of games removed:  93712
```

```python
# Generate a filtered_reviews dataframe which only contains reviews for games that are in the filtered_metadata dataframe
filtered_reviews = review_census[review_census['appid'].astype(int).isin(filtered_metadata['steam_appid'].astype(int))]
print("length of filtered_reviews: ", len(filtered_reviews))
```
```
length of filtered_reviews:  50072919
```

```python
# Drop duplicate texts from filtered_reviews
filtered_reviews = filtered_reviews.drop_duplicates(subset=['processed_review'])
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
length of filtered_reviews after unique word count pass:  18972937
length of filtered_reviews after received for free pass:  18830534
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
54408  2425310                 2
54409  2425400                 3
54410  2426330                 1
54411  2427360                 4
54412  2432780                 1

[54413 rows x 2 columns]
```

```python
# Filter out games with fewer than 20 qualifying reviews
filtered_reviews = filtered_reviews[filtered_reviews['appid'].isin(review_counts[review_counts['processed_review'] >= 20]['appid'])]
filtered_reviews.reset_index(inplace=True, drop=True)
print("length of filtered_reviews after review count pass: ", len(filtered_reviews))
```
```
length of filtered_reviews after review count pass:  18633890
```

```python
# Add time weighted resonance columns
filtered_reviews['days_since_review_posted'] = (pd.to_datetime('2023-06-01') - filtered_reviews['datetime_timestamp_updated']).dt.days
filtered_reviews['log365_days_since_review'] = filtered_reviews['days_since_review_posted'].apply(lambda x: math.log(x, 365)).clip(lower=1)
filtered_reviews['time_weighted_resonance'] = filtered_reviews['resonance_score'] / filtered_reviews['log365_days_since_review']
```

```python
# Finally, filter filtered_metadata to only contain games that have reviews in filtered_reviews
filtered_metadata = filtered_metadata[filtered_metadata['steam_appid'].astype(int).isin(filtered_reviews['appid'].astype(int))]
```

#### Save Point

```python
# Optional: Save filtered metadata and filtered reviews
filtered_metadata.to_feather(os.path.join(db_generation_folder, 'filtered_metadata_May31_2023.feather' ))
```

```python
filtered_reviews.to_feather(os.path.join(db_generation_folder, 'filtered_reviews_May31_2023.feather'))
```

#### Load Point 

```python
# Optional: Load filtered metadata and filtered reviews
filtered_metadata = pd.read_feather(os.path.join(db_generation_folder, 'filtered_metadata_May31_2023.feather'))
```

```python
# Optional: Load filtered metadata and filtered reviews
filtered_reviews = pd.read_feather(os.path.join(db_generation_folder, 'filtered_reviews_May31_2023.feather'))
```

#### Experiments with Average Embeddings to Get A Single Embedding Per Game

```python
deeper_embeddings = pd.read_pickle(os.path.join(review_embeddings_output_folder, '307110.pickle'))
```

```python
# TEST: Iterate over deeper_embeddings and print both the recommendationid and the embedding
for recommendationid, embedding in deeper_embeddings:
    print(recommendationid)
    print(embedding)
    print()
```
```
...
9922897
[ 0.00263938 -0.01317883  0.00428345 ... -0.00047795 -0.009283
 -0.03368945]

33297795
[-0.03038037 -0.01759285  0.01397551 ... -0.01895436 -0.01130588
 -0.00618019]

36378859
[-0.00194332 -0.01048745  0.02079716 ... -0.01442538  0.00675805
 -0.00325938]

116447367
[ 0.00522446 -0.04789724  0.00659731 ... -0.00232622 -0.01469921
 -0.01474081]

60439701
[-0.00630615 -0.02327069  0.0051494  ... -0.01229344 -0.01223916
 -0.03058433]

35560426
[-1.0861144e-02 -3.1790748e-02  3.0890289e-03 ...  1.1220981e-03
 -4.6011668e-05 -1.5645053e-02]

64393654
[ 0.01096444 -0.02755784  0.02200892 ... -0.01437916 -0.00796323
 -0.00964391]
```

```python
deeper_embeddings['embedding']
```
```
array([[-3.9991066e-03, -3.9553355e-02,  1.2580938e-02, ...,
        -4.0123709e-03, -5.1862379e-03,  2.6096990e-03],
       [-6.7104208e-03, -2.7109833e-02,  6.6701989e-03, ...,
        -1.0283503e-02, -2.2015007e-02,  1.7603962e-02],
       [-6.1722286e-04, -3.2049522e-02,  1.2209666e-02, ...,
        -1.5082930e-02, -2.8749704e-03, -1.7389731e-02],
       ...,
       [-6.3061528e-03, -2.3270687e-02,  5.1494027e-03, ...,
        -1.2293436e-02, -1.2239160e-02, -3.0584332e-02],
       [-1.0861144e-02, -3.1790748e-02,  3.0890289e-03, ...,
         1.1220981e-03, -4.6011668e-05, -1.5645053e-02],
       [ 1.0964445e-02, -2.7557839e-02,  2.2008922e-02, ...,
        -1.4379163e-02, -7.9632280e-03, -9.6439095e-03]], dtype=float32)
```

```python
np.average(deeper_embeddings['embedding'], axis=0)
```
```
array([-0.0049038 , -0.02107454,  0.00883601, ..., -0.01418478,
       -0.00682329, -0.01515096], dtype=float32)
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
       'datetime_timestamp_updated', 'steam_release_year', 'appid',
       'days_since_review_posted', 'log365_days_since_review',
       'time_weighted_resonance'],
      dtype='object')
```

#### Generate Embeddings Specific To Metadata - Game-Level Embeddings

```python
# COMMENTED OUT: RUN ONCE, THEN IT'S SAVED. WOOHOO! 
# If we ever need to generate game-specific embeddings again, we can use this code.

# for row_index, row_entry in filtered_metadata.iterrows():
#     appid = row_entry['steam_appid']

#     game_title = str(row_entry['name'])
#     game_developers = str(row_entry['developers'])
#     game_publishers = str(row_entry['publishers'])
#     game_short_description = preprocess_text(row_entry['short_description'])
#     about_the_game = preprocess_text(row_entry['about_the_game'])

#     game_data_embedding_string = game_title + ' \n ' + game_developers + ' \n ' + game_publishers + ' \n ' + game_short_description + " \n " + about_the_game
#     game_data_embedding_string = shorten_text_to_max_tokens(game_data_embedding_string, token_limit=8100)
#     game_data_embedding = embed(game_data_embedding_string)

#     # Save embedding to disk as a pickle file named after the appid
#     with open(os.path.join(metadata_embeddings_output_folder, f'{appid}.pickle'), 'wb') as f:
#         pickle.dump(game_data_embedding, f)
```

```python
# Initialize an empty DataFrame to hold the appid and average embedding
game_embeddings_df = pd.DataFrame(columns=['steam_appid', 'embedding'])

# Group the filtered_reviews DataFrame by 'appid'
grouped_reviews = filtered_reviews.groupby('appid')

# Loop through each group in the grouped DataFrame
for appid, group in grouped_reviews:

    if not os.path.exists(os.path.join(review_embeddings_output_folder, f'{appid}.pickle')):
        print(f'No reviews-level embedding file found for appid {appid}.')
        continue

    if not os.path.exists(os.path.join(metadata_embeddings_output_folder, f'{appid}.pickle')):
        print(f'No metadata-level embedding file found for appid {appid}.')
        continue

    # Load the corresponding pickle file
    review_embeddings = pd.read_pickle(os.path.join(review_embeddings_output_folder, f'{appid}.pickle'))
    metadata_embedding = pd.read_pickle(os.path.join(metadata_embeddings_output_folder, f'{appid}.pickle'))

    # Convert the embeddings list to a DataFrame
    recommendationids = [item[0] for item in review_embeddings]
    embeddings_list = [item[1] for item in review_embeddings]
    embeddings_df = pd.DataFrame({'recommendationid': recommendationids, 'embedding': embeddings_list})
    
    # Match each 'recommendationid' in the embeddings with its 'time_weighted_resonance' in group
    merged = pd.merge(embeddings_df, group[['recommendationid', 'time_weighted_resonance']], on='recommendationid')
    
    # Calculate the weighted average REVIEW embedding
    weights = merged['time_weighted_resonance'].values
    embeddings_array = np.stack(merged['embedding'].values)
    average_review_embedding = np.average(embeddings_array, weights=weights, axis=0)

    # Now average the averaged REVIEW embeding with the METADATA embedding. The audience gets 95% of the weight, and the developer gets 5% of the weight.
    average_embedding = np.average(np.stack([average_review_embedding, metadata_embedding]), weights = [0.95, 0.05], axis=0)

    # Append the appid and average_embedding to the game_embeddings_df DataFrame
    game_embeddings_df = pd.concat([game_embeddings_df, pd.DataFrame({'steam_appid': [appid], 'embedding': [average_embedding]})], ignore_index=True)
    
    # Save the average embedding to a new pickle file named after its appid
    with open(os.path.join(game_embeddings_output_folder, f'{appid}.pickle'), 'wb') as f:
        pickle.dump(average_embedding, f)

```

```python
# Merge the game_embeddings_df DataFrame with the filtered_metadata DataFrame
filtered_metadata_with_embeddings = pd.merge(filtered_metadata, game_embeddings_df, on='steam_appid')
```

```python
filtered_metadata_with_embeddings
```
```
       type                               name steam_appid required_age  \
0      game                     Counter-Strike          10            0   
1      game     Counter-Strike: Condition Zero          80            0   
2      game                        Crown Trick     1000010            0   
3      game        Cook, Serve, Delicious! 3?!     1000030            0   
4      game                            Zengeon     1000080            0   
...     ...                                ...         ...          ...   
21960  game                   Amnesia: Rebirth      999220            0   
21961  game                           Magnibox      999410            0   
21962  game                       Cube Defense      999640            0   
21963  game  SAMURAI SHODOWN NEOGEO COLLECTION      999660            0   
21964  game                     Enemy On Board      999860            0   

       is_free                               detailed_description  \
0        False  Play the world's number 1 online action game. ...   
1        False  With its extensive Tour of Duty campaign, a ne...   
2        False  <img src="https://cdn.akamai.steamstatic.com/s...   
3        False  <h1>Join our Discord Channel!</h1><p><a href="...   
4        False  <h1>CHECK OUT OTHER GAMES FROM 2P GAMES</h1><p...   
...        ...                                                ...   
21960    False  <h1>Frictional Games</h1><p><a href="https://s...   
21961    False  Magnibox is a colourful and challenging puzzle...   
21962    False  <img src="https://cdn.akamai.steamstatic.com/s...   
21963    False  The ULTIMATE collection is here!<br>It all beg...   
21964     True  <img src="https://cdn.akamai.steamstatic.com/s...   

                                          about_the_game  \
0      Play the world's number 1 online action game. ...   
1      With its extensive Tour of Duty campaign, a ne...   
2      <img src="https://cdn.akamai.steamstatic.com/s...   
3      Hit the road in this massive sequel to the mil...   
4      <img src="https://cdn.akamai.steamstatic.com/s...   
...                                                  ...   
21960  <img src="https://cdn.akamai.steamstatic.com/s...   
21961  Magnibox is a colourful and challenging puzzle...   
21962  <img src="https://cdn.akamai.steamstatic.com/s...   
21963  The ULTIMATE collection is here!<br>It all beg...   
21964  <img src="https://cdn.akamai.steamstatic.com/s...   

                                       short_description  \
0      Play the world's number 1 online action game. ...   
1      With its extensive Tour of Duty campaign, a ne...   
2      Enter a labyrinth that moves as you move, wher...   
3      Cook, serve and manage your food truck as you ...   
4      Zengeon is an anime infused Action RPG and Rog...   
...                                                  ...   
21960  A first-person horror adventure. Uncover your ...   
21961  A puzzle game about magnets designed for human...   
21962  You left alone camping in the mountains ... Bu...   
21963  The ULTIMATE collection is here! It all began ...   
21964  Online (6v2) social deduction game that will r...   

                                     supported_languages  \
0      English<strong>*</strong>, French<strong>*</st...   
1      English, French, German, Italian, Spanish - Sp...   
2      Simplified Chinese, English, Japanese, Traditi...   
3      English<strong>*</strong><br><strong>*</strong...   
4      Simplified Chinese, English, Traditional Chine...   
...                                                  ...   
21960  English<strong>*</strong>, French, Italian, Ge...   
21961                                            English   
21962                                            English   
21963  English, French, Italian, German, Japanese<str...   
21964  English<strong>*</strong><br><strong>*</strong...   

                                            header_image  ... fullgame.name  \
0      https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
1      https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
2      https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
3      https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
4      https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
...                                                  ...  ...           ...   
21960  https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
21961  https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
21962  https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
21963  https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
21964  https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   

      ext_user_account_notice pc_requirements demos drm_notice  \
0                        None            None  None       None   
1                        None            None  None       None   
2                        None            None  None       None   
3                        None            None  None       None   
4                        None            None  None       None   
...                       ...             ...   ...        ...   
21960                    None            None  None       None   
21961                    None            None  None       None   
21962                    None            None  None       None   
21963                    None            None  None       None   
21964                    None            None  None       None   

      alternate_appid price_overview.recurring_sub  \
0                None                          NaN   
1                None                          NaN   
2                None                          NaN   
3                None                          NaN   
4                None                          NaN   
...               ...                          ...   
21960            None                          NaN   
21961            None                          NaN   
21962            None                          NaN   
21963            None                          NaN   
21964            None                          NaN   

      price_overview.recurring_sub_desc    appid  \
0                                  None       10   
1                                  None       80   
2                                  None  1000010   
3                                  None  1000030   
4                                  None  1000080   
...                                 ...      ...   
21960                              None   999220   
21961                              None   999410   
21962                              None   999640   
21963                              None   999660   
21964                              None   999860   

                                               embedding  
0      [-0.012578621694776658, -0.009103613677506336,...  
1      [-0.014615686521472193, -0.009386611280343496,...  
2      [-0.011608901891584669, -0.022129474824656437,...  
3      [0.00478626255020513, -0.020901394019746763, -...  
4      [-0.01271487719461865, -0.02223631292066681, 0...  
...                                                  ...  
21960  [-0.004974830418665052, -0.016126159396253063,...  
21961  [-0.010181532104584806, -0.017516571611220857,...  
21962  [-0.016621434568584906, -0.016817071767740027,...  
21963  [-0.01459810543925751, -0.019572433575863205, ...  
21964  [-0.002927530587152568, -0.016823961052607278,...  

[21965 rows x 65 columns]
```

#### UMAP And Nearest Neighbors

```python
import umap
reducer = umap.UMAP(n_neighbors=20, min_dist=0.1, n_components=3, metric="euclidean")
```

```python
# Create a list of embeddings indexed by appid
game_embeddings_df
```
```
      steam_appid                                          embedding
0              10  [-0.012578621694776658, -0.009103613677506336,...
1              20  [-0.011349836498944024, -0.017281235492983937,...
2              30  [-0.018167533520471086, -0.014313393175601702,...
3              40  [-0.01599997144791641, -0.014600798404979813, ...
4              50  [-0.012145895287528777, -0.013695432529850811,...
...           ...                                                ...
21960     2381590  [-0.008004964116373161, -0.016571832629991453,...
21961     2389240  [-0.013240193413309466, -0.006364147467156854,...
21962     2400280  [-0.012043038004358695, -0.02075078005426564, ...
21963     2401460  [-0.008008861601436596, -0.02117697392071106, ...
21964     2407350  [-0.004294560677833354, -0.02161493198536278, ...

[21965 rows x 2 columns]
```

```python
indexed_embeddings = game_embeddings_df.set_index('steam_appid')
indexed_embeddings
```
```
                                                     embedding
steam_appid                                                   
10           [-0.012578621694776658, -0.009103613677506336,...
20           [-0.011349836498944024, -0.017281235492983937,...
30           [-0.018167533520471086, -0.014313393175601702,...
40           [-0.01599997144791641, -0.014600798404979813, ...
50           [-0.012145895287528777, -0.013695432529850811,...
...                                                        ...
2381590      [-0.008004964116373161, -0.016571832629991453,...
2389240      [-0.013240193413309466, -0.006364147467156854,...
2400280      [-0.012043038004358695, -0.02075078005426564, ...
2401460      [-0.008008861601436596, -0.02117697392071106, ...
2407350      [-0.004294560677833354, -0.02161493198536278, ...

[21965 rows x 1 columns]
```

```python
umapped_games = reducer.fit_transform(indexed_embeddings['embedding'].values.tolist())
umapped_games.shape
```
```
(21965, 3)
```

```python
umapped_games
```
```
array([[ 1.4911673 ,  5.227669  ,  9.844     ],
       [ 1.5891237 ,  5.261552  ,  9.715622  ],
       [ 1.8358066 ,  5.3750134 , 10.361325  ],
       ...,
       [ 0.50550246, -1.7977718 , 12.3383665 ],
       [-1.7362986 ,  0.04153662,  8.94353   ],
       [-1.3516674 ,  0.5583774 ,  9.477545  ]], dtype=float32)
```

```
plt.scatter(
    umapped_games[:, 0],
    umapped_games[:, 1],
    umapped_games[:, 2])
plt.gca().set_aspect('equal', 'datalim')
plt.title('UMAP projection of the Games dataset', fontsize=24)
```
![[Pasted image 20240929225634.png]]

```python
# Stack all embeddings into a single matrix
embeddings_array = np.stack(indexed_embeddings['embedding'].values)
```

```python
neighbor_graph = umap.umap_.nearest_neighbors(embeddings_array, n_neighbors = 21, verbose=True, metric = "euclidean", random_state=np.random.RandomState(seed=42), angular=False, metric_kwds={})
```
```
Wed Jun 14 23:53:04 2023 Finding Nearest Neighbors
Wed Jun 14 23:53:04 2023 Building RP forest with 12 trees
Wed Jun 14 23:53:05 2023 NN descent for 14 iterations
	 1  /  14
	 2  /  14
	 3  /  14
	 4  /  14
	Stopping threshold met -- exiting after 4 iterations
Wed Jun 14 23:53:06 2023 Finished Nearest Neighbor Search
```

```python
neighbor_graph
```
```
(array([[    0,    10,    24, ..., 17410,  1397, 15512],
        [    1,    18,     2, ..., 11745,    45,  2824],
        [    2,    12,  6202, ...,  1082,  8804,  1812],
        ...,
        [21962, 21829, 21896, ..., 17807, 21625, 18785],
        [21963, 15732, 19253, ..., 19232, 16778, 16849],
        [21964, 19536, 21943, ..., 21901, 16115, 16951]]),
 array([[0.        , 0.10259055, 0.1363666 , ..., 0.2430806 , 0.24481818,
         0.2458352 ],
        [0.        , 0.13639581, 0.20820314, ..., 0.24066983, 0.24106464,
         0.24173932],
        [0.        , 0.09577244, 0.14592832, ..., 0.19939476, 0.19944729,
         0.1995744 ],
        ...,
        [0.        , 0.11058121, 0.13151817, ..., 0.17242157, 0.17300521,
         0.17353335],
        [0.        , 0.14820981, 0.15591186, ..., 0.17593102, 0.17618786,
         0.1763686 ],
        [0.        , 0.17422701, 0.18462175, ..., 0.22053066, 0.22186956,
         0.2229379 ]], dtype=float32),
 <pynndescent.pynndescent_.NNDescent at 0x2c713b25c60>)
```

```python
neighbor_graph[0]
```
```
array([[    0,    10,    24, ..., 17410,  1397, 15512],
       [    1,    18,     2, ..., 11745,    45,  2824],
       [    2,    12,  6202, ...,  1082,  8804,  1812],
       ...,
       [21962, 21829, 21896, ..., 17807, 21625, 18785],
       [21963, 15732, 19253, ..., 19232, 16778, 16849],
       [21964, 19536, 21943, ..., 21901, 16115, 16951]])
```

```python
# neighbor_graph[0] contains the INDEX of the 15 nearest neighbors for each game. Now we need to translate these indices into appids.
# First, create a dictionary that maps each index to its corresponding appid

index_to_appid = {index: appid for index, appid in enumerate(indexed_embeddings.index)}

index_to_appid
```
```
{0: 10,
 1: 20,
 2: 30,
 3: 40,
 4: 50,
 5: 60,
 6: 70,
 7: 80,
 8: 130,
 9: 220,
 10: 240,
 11: 280,
 12: 300,
 13: 320,
 14: 360,
 15: 380,
 16: 400,
 17: 420,
 18: 440,
 19: 500,
 20: 550,
 21: 570,
 22: 620,
 23: 630,
 24: 730,
...
 979: 104200,
 980: 104600,
 981: 104900,
 982: 105000,
 983: 105100,
 984: 105300,
 985: 105420,
 986: 105450,
 987: 105600,
 988: 105700,
 989: 105800,
 990: 107100,
 991: 107200,
 992: 107300,
 993: 107310,
 994: 107410,
 995: 107600,
 996: 108110,
 997: 108200,
 998: 108500,
 999: 108600,
 ...}
```

```python
# Now, create a dictionary that maps each appid to its 15 nearest neighbors
appid_to_neighbors = {index_to_appid[index]: [index_to_appid[neighbor_index] for neighbor_index in neighbor_indices] for index, neighbor_indices in enumerate(neighbor_graph[0])}
```

```python
appid_to_neighbors
```
```
...
108500: [108500,
  211180,
  1528050,
  700160,
  331510,
  246980,
  1441180,
  18700,
  337490,
  640120,
  1107750,
  295750,
  255870,
  222140,
  556240,
  751220,
  290510,
  218740,
  812160,
  1852830,
  57200],
 108600: [108600,
  1054510,
  251570,
  1377380,
  1963370,
  495420,
  360170,
  221100,
  250400,
  329430,
  238430,
  914620,
  1645820,
  541300,
  764920,
  340050,
  699130,
  1266840,
  339230,
  1262460,
  513710],
 ...}
```

```python
appid_to_neighbors[307110]
```
```
[307110,
 1286580,
 976890,
 1016360,
 867400,
 1132030,
 1412850,
 1756160,
 858590,
 1162470,
 1451480,
 1061880,
 1627850,
 263920,
 342620,
 1040460,
 1343620,
 271570,
 1428470,
 377140,
 240620]
```

```python
for appid in appid_to_neighbors[307110]:
    print("https://store.steampowered.com/app/{appid}".format(appid=appid))
```
```
https://store.steampowered.com/app/307110
https://store.steampowered.com/app/1286580
https://store.steampowered.com/app/976890
https://store.steampowered.com/app/1016360
https://store.steampowered.com/app/867400
https://store.steampowered.com/app/1132030
https://store.steampowered.com/app/1412850
https://store.steampowered.com/app/1756160
https://store.steampowered.com/app/858590
https://store.steampowered.com/app/1162470
https://store.steampowered.com/app/1451480
https://store.steampowered.com/app/1061880
https://store.steampowered.com/app/1627850
https://store.steampowered.com/app/263920
https://store.steampowered.com/app/342620
https://store.steampowered.com/app/1040460
https://store.steampowered.com/app/1343620
https://store.steampowered.com/app/271570
https://store.steampowered.com/app/1428470
https://store.steampowered.com/app/377140
https://store.steampowered.com/app/240620
```

```python
filtered_metadata_with_embeddings
```
```
       type                               name steam_appid required_age  \
0      game                     Counter-Strike          10            0   
1      game     Counter-Strike: Condition Zero          80            0   
2      game                        Crown Trick     1000010            0   
3      game        Cook, Serve, Delicious! 3?!     1000030            0   
4      game                            Zengeon     1000080            0   
...     ...                                ...         ...          ...   
21960  game                   Amnesia: Rebirth      999220            0   
21961  game                           Magnibox      999410            0   
21962  game                       Cube Defense      999640            0   
21963  game  SAMURAI SHODOWN NEOGEO COLLECTION      999660            0   
21964  game                     Enemy On Board      999860            0   

       is_free                               detailed_description  \
0        False  Play the world's number 1 online action game. ...   
1        False  With its extensive Tour of Duty campaign, a ne...   
2        False  <img src="https://cdn.akamai.steamstatic.com/s...   
3        False  <h1>Join our Discord Channel!</h1><p><a href="...   
4        False  <h1>CHECK OUT OTHER GAMES FROM 2P GAMES</h1><p...   
...        ...                                                ...   
21960    False  <h1>Frictional Games</h1><p><a href="https://s...   
21961    False  Magnibox is a colourful and challenging puzzle...   
21962    False  <img src="https://cdn.akamai.steamstatic.com/s...   
21963    False  The ULTIMATE collection is here!<br>It all beg...   
21964     True  <img src="https://cdn.akamai.steamstatic.com/s...   

                                          about_the_game  \
0      Play the world's number 1 online action game. ...   
1      With its extensive Tour of Duty campaign, a ne...   
2      <img src="https://cdn.akamai.steamstatic.com/s...   
3      Hit the road in this massive sequel to the mil...   
4      <img src="https://cdn.akamai.steamstatic.com/s...   
...                                                  ...   
21960  <img src="https://cdn.akamai.steamstatic.com/s...   
21961  Magnibox is a colourful and challenging puzzle...   
21962  <img src="https://cdn.akamai.steamstatic.com/s...   
21963  The ULTIMATE collection is here!<br>It all beg...   
21964  <img src="https://cdn.akamai.steamstatic.com/s...   

                                       short_description  \
0      Play the world's number 1 online action game. ...   
1      With its extensive Tour of Duty campaign, a ne...   
2      Enter a labyrinth that moves as you move, wher...   
3      Cook, serve and manage your food truck as you ...   
4      Zengeon is an anime infused Action RPG and Rog...   
...                                                  ...   
21960  A first-person horror adventure. Uncover your ...   
21961  A puzzle game about magnets designed for human...   
21962  You left alone camping in the mountains ... Bu...   
21963  The ULTIMATE collection is here! It all began ...   
21964  Online (6v2) social deduction game that will r...   

                                     supported_languages  \
0      English<strong>*</strong>, French<strong>*</st...   
1      English, French, German, Italian, Spanish - Sp...   
2      Simplified Chinese, English, Japanese, Traditi...   
3      English<strong>*</strong><br><strong>*</strong...   
4      Simplified Chinese, English, Traditional Chine...   
...                                                  ...   
21960  English<strong>*</strong>, French, Italian, Ge...   
21961                                            English   
21962                                            English   
21963  English, French, Italian, German, Japanese<str...   
21964  English<strong>*</strong><br><strong>*</strong...   

                                            header_image  ... fullgame.name  \
0      https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
1      https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
2      https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
3      https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
4      https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
...                                                  ...  ...           ...   
21960  https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
21961  https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
21962  https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
21963  https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   
21964  https://cdn.akamai.steamstatic.com/steam/apps/...  ...          None   

      ext_user_account_notice pc_requirements demos drm_notice  \
0                        None            None  None       None   
1                        None            None  None       None   
2                        None            None  None       None   
3                        None            None  None       None   
4                        None            None  None       None   
...                       ...             ...   ...        ...   
21960                    None            None  None       None   
21961                    None            None  None       None   
21962                    None            None  None       None   
21963                    None            None  None       None   
21964                    None            None  None       None   

      alternate_appid price_overview.recurring_sub  \
0                None                          NaN   
1                None                          NaN   
2                None                          NaN   
3                None                          NaN   
4                None                          NaN   
...               ...                          ...   
21960            None                          NaN   
21961            None                          NaN   
21962            None                          NaN   
21963            None                          NaN   
21964            None                          NaN   

      price_overview.recurring_sub_desc    appid  \
0                                  None       10   
1                                  None       80   
2                                  None  1000010   
3                                  None  1000030   
4                                  None  1000080   
...                                 ...      ...   
21960                              None   999220   
21961                              None   999410   
21962                              None   999640   
21963                              None   999660   
21964                              None   999860   

                                               embedding  
0      [-0.012578621694776658, -0.009103613677506336,...  
1      [-0.014615686521472193, -0.009386611280343496,...  
2      [-0.011608901891584669, -0.022129474824656437,...  
3      [0.00478626255020513, -0.020901394019746763, -...  
4      [-0.01271487719461865, -0.02223631292066681, 0...  
...                                                  ...  
21960  [-0.004974830418665052, -0.016126159396253063,...  
21961  [-0.010181532104584806, -0.017516571611220857,...  
21962  [-0.016621434568584906, -0.016817071767740027,...  
21963  [-0.01459810543925751, -0.019572433575863205, ...  
21964  [-0.002927530587152568, -0.016823961052607278,...  

[21965 rows x 65 columns]
```

```python
appid_to_neighbors.keys()
```
```
dict_keys([10, 20, 30, 40, 50, 60, 70, 80, 130, 220, 240, 280, 300, 320, 360, 380, 400, 420, 440, 500, 550, 570, 620, 630, 730, 1002, 1200, 1250, 1300,
...
```

```python
appid_to_neighbors_df_column = pd.DataFrame(columns=['appid', 'nearest_neighbors'])

for key, value in appid_to_neighbors.items():
    nn_string = '|'.join([str(appid) for appid in value[1:]]) # Skip the first entry, which is the appid itself
    add_entry = pd.DataFrame({'appid': [key], 'nearest_neighbors': [nn_string]})
    appid_to_neighbors_df_column = pd.concat([appid_to_neighbors_df_column, add_entry], ignore_index=True)

appid_to_neighbors_df_column.set_index('appid', inplace=True)
appid_to_neighbors_df_column
```
```
                                         nearest_neighbors
appid                                                     
10       240|730|80|273110|391460|300|360|4000|320|30|5...
20       440|30|63200|40|333930|320|42120|17710|360|101...
30       300|454350|1012110|1200|447820|703320|32770|74...
40       360|320|2200|282440|30|321260|225840|42120|632...
50       130|399120|70|280|220|380|723390|362890|420|59...
...                                                    ...
2381590  575050|1547160|1057780|1381530|913550|1899400|...
2389240  1449000|405010|2178570|1830870|553790|1023980|...
2400280  2258590|2304030|1457620|2137630|1437830|205981...
2401460  1224160|1639990|1357670|1571170|1493730|139410...
2407350  1687250|2351090|2331960|1310910|2316410|100122...

[21965 rows x 1 columns]
```

```python
# Add the appid_to_neighbors_df_column to the filtered_metadata_with_embeddings DataFrame
filtered_metadata_with_embeddings = pd.merge(filtered_metadata_with_embeddings, appid_to_neighbors_df_column, on='appid')
```

```python
filtered_metadata_with_embeddings
```
```
       type                               name steam_appid required_age  \
0      game                     Counter-Strike          10            0   
1      game     Counter-Strike: Condition Zero          80            0   
2      game                        Crown Trick     1000010            0   
3      game        Cook, Serve, Delicious! 3?!     1000030            0   
4      game                            Zengeon     1000080            0   
...     ...                                ...         ...          ...   
21960  game                   Amnesia: Rebirth      999220            0   
21961  game                           Magnibox      999410            0   
21962  game                       Cube Defense      999640            0   
21963  game  SAMURAI SHODOWN NEOGEO COLLECTION      999660            0   
21964  game                     Enemy On Board      999860            0   

       is_free                               detailed_description  \
0        False  Play the world's number 1 online action game. ...   
1        False  With its extensive Tour of Duty campaign, a ne...   
2        False  <img src="https://cdn.akamai.steamstatic.com/s...   
3        False  <h1>Join our Discord Channel!</h1><p><a href="...   
4        False  <h1>CHECK OUT OTHER GAMES FROM 2P GAMES</h1><p...   
...        ...                                                ...   
21960    False  <h1>Frictional Games</h1><p><a href="https://s...   
21961    False  Magnibox is a colourful and challenging puzzle...   
21962    False  <img src="https://cdn.akamai.steamstatic.com/s...   
21963    False  The ULTIMATE collection is here!<br>It all beg...   
21964     True  <img src="https://cdn.akamai.steamstatic.com/s...   

                                          about_the_game  \
0      Play the world's number 1 online action game. ...   
1      With its extensive Tour of Duty campaign, a ne...   
2      <img src="https://cdn.akamai.steamstatic.com/s...   
3      Hit the road in this massive sequel to the mil...   
4      <img src="https://cdn.akamai.steamstatic.com/s...   
...                                                  ...   
21960  <img src="https://cdn.akamai.steamstatic.com/s...   
21961  Magnibox is a colourful and challenging puzzle...   
21962  <img src="https://cdn.akamai.steamstatic.com/s...   
21963  The ULTIMATE collection is here!<br>It all beg...   
21964  <img src="https://cdn.akamai.steamstatic.com/s...   

                                       short_description  \
0      Play the world's number 1 online action game. ...   
1      With its extensive Tour of Duty campaign, a ne...   
2      Enter a labyrinth that moves as you move, wher...   
3      Cook, serve and manage your food truck as you ...   
4      Zengeon is an anime infused Action RPG and Rog...   
...                                                  ...   
21960  A first-person horror adventure. Uncover your ...   
21961  A puzzle game about magnets designed for human...   
21962  You left alone camping in the mountains ... Bu...   
21963  The ULTIMATE collection is here! It all began ...   
21964  Online (6v2) social deduction game that will r...   

                                     supported_languages  \
0      English<strong>*</strong>, French<strong>*</st...   
1      English, French, German, Italian, Spanish - Sp...   
2      Simplified Chinese, English, Japanese, Traditi...   
3      English<strong>*</strong><br><strong>*</strong...   
4      Simplified Chinese, English, Traditional Chine...   
...                                                  ...   
21960  English<strong>*</strong>, French, Italian, Ge...   
21961                                            English   
21962                                            English   
21963  English, French, Italian, German, Japanese<str...   
21964  English<strong>*</strong><br><strong>*</strong...   

                                            header_image  ...  \
0      https://cdn.akamai.steamstatic.com/steam/apps/...  ...   
1      https://cdn.akamai.steamstatic.com/steam/apps/...  ...   
2      https://cdn.akamai.steamstatic.com/steam/apps/...  ...   
3      https://cdn.akamai.steamstatic.com/steam/apps/...  ...   
4      https://cdn.akamai.steamstatic.com/steam/apps/...  ...   
...                                                  ...  ...   
21960  https://cdn.akamai.steamstatic.com/steam/apps/...  ...   
21961  https://cdn.akamai.steamstatic.com/steam/apps/...  ...   
21962  https://cdn.akamai.steamstatic.com/steam/apps/...  ...   
21963  https://cdn.akamai.steamstatic.com/steam/apps/...  ...   
21964  https://cdn.akamai.steamstatic.com/steam/apps/...  ...   

      ext_user_account_notice pc_requirements demos drm_notice  \
0                        None            None  None       None   
1                        None            None  None       None   
2                        None            None  None       None   
3                        None            None  None       None   
4                        None            None  None       None   
...                       ...             ...   ...        ...   
21960                    None            None  None       None   
21961                    None            None  None       None   
21962                    None            None  None       None   
21963                    None            None  None       None   
21964                    None            None  None       None   

      alternate_appid price_overview.recurring_sub  \
0                None                          NaN   
1                None                          NaN   
2                None                          NaN   
3                None                          NaN   
4                None                          NaN   
...               ...                          ...   
21960            None                          NaN   
21961            None                          NaN   
21962            None                          NaN   
21963            None                          NaN   
21964            None                          NaN   

      price_overview.recurring_sub_desc    appid  \
0                                  None       10   
1                                  None       80   
2                                  None  1000010   
3                                  None  1000030   
4                                  None  1000080   
...                                 ...      ...   
21960                              None   999220   
21961                              None   999410   
21962                              None   999640   
21963                              None   999660   
21964                              None   999860   

                                               embedding  \
0      [-0.012578621694776658, -0.009103613677506336,...   
1      [-0.014615686521472193, -0.009386611280343496,...   
2      [-0.011608901891584669, -0.022129474824656437,...   
3      [0.00478626255020513, -0.020901394019746763, -...   
4      [-0.01271487719461865, -0.02223631292066681, 0...   
...                                                  ...   
21960  [-0.004974830418665052, -0.016126159396253063,...   
21961  [-0.010181532104584806, -0.017516571611220857,...   
21962  [-0.016621434568584906, -0.016817071767740027,...   
21963  [-0.01459810543925751, -0.019572433575863205, ...   
21964  [-0.002927530587152568, -0.016823961052607278,...   

                                       nearest_neighbors  
0      240|730|80|273110|391460|300|360|4000|320|30|5...  
1      240|10|730|273110|300|30|1012110|391460|222880...  
2      1016600|434650|844850|1000080|788100|1260810|1...  
3      386620|247020|1928090|846800|1122340|1599600|1...  
4      610960|973450|843720|1641670|635250|1090440|73...  
...                                                  ...  
21960  57300|239200|282140|22180|633360|715380|112159...  
21961  454320|235980|858760|642560|867380|676420|3192...  
21962  1739880|342300|440730|945460|654850|273500|915...  
21963  1076550|702110|465840|885150|1515950|865940|16...  
21964  953880|408900|1946300|1428470|1497640|1343620|...  

[21965 rows x 66 columns]
```

#### Test: Category Labeling Bools

```python
# Test: go through filtered_metadata_with_embeddings and try generating category bools for each game
for index, row in filtered_metadata_with_embeddings.iterrows():
    print(category_tags_to_bools(row, category_tag_table = category_tag_table))
    break

```
```
{'Multi-player': True, 'Single-player': False, 'Co-op': False, 'Captions available': False, 'Partial Controller Support': False, 'Mods': False, 'MMO': False, 'Steam Achievements': False, 'Steam Cloud': False, 'Shared/Split Screen': False, 'Cross-Platform Multiplayer': False, 'Full controller support': False, 'Steam Workshop': False, 'VR Support': False, 'Native Steam Controller Support': False, 'In-App Purchases': False, 'Online PvP': True, 'Shared/Split Screen PvP': True, 'Online Co-op': False, 'Shared/Split Screen Co-op': False, 'Remote Play on Phone': False, 'Remote Play on Tablet': False, 'Remote Play on TV': False, 'Remote Play Together': False, 'LAN PvP': False, 'LAN Co-op': False, 'PvP': True, 'Tracked Controller Support': False, 'VR Supported': False, 'VR Only': False}
```

#### Upload Games Collection to FREE Zilliz Instance

```python
from pymilvus import connections, FieldSchema, CollectionSchema, DataType, Collection, utility
```

```python
# Connect to Zilliz Cloud
connections.connect("default", uri=ZILLIZ_FREE_URI, token=ZILLIZ_API_KEY, secure=True)
```

```python
##### !!!! OPTIONAL !!!! #####
##### THIS CODE DELETES EXISTING COLLECTIONS SO YOU CAN UPLOAD NEW ONES #####
# Remove collection if it already exists
if utility.has_collection(GAME_COLLECTION_NAME):
    utility.drop_collection(GAME_COLLECTION_NAME)
```

```python
game_fields = [
    FieldSchema(name="appid", dtype=DataType.INT64, is_primary=True, description='Steam AppID', auto_id=False),
    FieldSchema(name="embedding", dtype=DataType.FLOAT_VECTOR, description="Weighted embedding of reviews and game title, developers, publishers, short description, and about the game", dim=DIMENSION),
    FieldSchema(name="game_title", dtype=DataType.VARCHAR, description='Game Title', max_length=4096),
    FieldSchema(name="inferred_release_year", dtype=DataType.INT32, description='Inferred Release Year'),
    FieldSchema(name="game_developers", dtype=DataType.VARCHAR, description='Game Developers', max_length=4096),
    FieldSchema(name="game_publishers", dtype=DataType.VARCHAR, description='Game Publishers', max_length=4096),
    FieldSchema(name="game_short_description", dtype=DataType.VARCHAR, description='Short Description', max_length=20000),
    FieldSchema(name="related_appids", dtype=DataType.VARCHAR, description='Most similar games by Euclidean distance', max_length=1024),                    # NEW. This is the list of related appids, as determined by Euclidean distance; appids as strings separated by a | spacer.
    FieldSchema(name="game_detailed_description", dtype=DataType.VARCHAR, description='Detailed Description', max_length=65535),                            # NOTE: Turns out the detailed_description field in Steam metadata is often populated with Discord invites and merchandising. Try about_the_game instead.
    FieldSchema(name="about_the_game", dtype=DataType.VARCHAR, description='About The Game', max_length=65535),                                             # NEW. This is more frequently actually describing the game than the detailed_description field.
    #FieldSchema(name="game_controller_support", dtype=DataType.VARCHAR, description='Controller Support', max_length=512),                                 # REMOVED. This has become part of the category bools. 
    FieldSchema(name="game_supported_languages", dtype=DataType.VARCHAR, description='Supported Languages', max_length=4096),
    #FieldSchema(name="game_total_review_count", dtype=DataType.INT32, description='Total Review Count'),                                                   # RENAMED. This is now legit_review_count for further clarity.
    FieldSchema(name="legit_review_count", dtype=DataType.INT32, description='Number of reviews passing deduplication and word count filters'),             # NEW. This is the renamed replacement for total_review_count. It implies that discrepancy between our value and Steam's value is expected and normal.
    FieldSchema(name="legit_review_positivity", dtype=DataType.FLOAT, description='0 to 1 positivity score of legit reviews'),                              # NEW. This is the average positivity score of legit reviews and is a stand-in for the game's overall positivity score.
    FieldSchema(name="game_stats_gmean_word_count", dtype=DataType.FLOAT, description='GMean Reviews Word Count'),
    FieldSchema(name="game_stats_gmean_unique_word_count", dtype=DataType.FLOAT, description='GMean Reviews Unique Word Count'),
    FieldSchema(name="game_stats_gmean_hours_played", dtype=DataType.FLOAT, description='GMean Reviews Hours Played'),
    FieldSchema(name="game_stats_gmean_author_num_reviews", dtype=DataType.FLOAT, description='GMean Reviews Author Num Reviews'),
    FieldSchema(name="game_stats_gmean_num_games_owned", dtype=DataType.FLOAT, description='GMean Reviews Author Num Games Owned'),                         # NEW. This is the average number of games owned by the authors of the reviews.
    FieldSchema(name="game_stats_gmean_resonance", dtype=DataType.FLOAT, description='GMean Reviews Resonance'),                                            # NOTE: If you want to use a timeless measurement, use this one.
    FieldSchema(name="game_stats_gmean_time_weighted_resonance", dtype=DataType.FLOAT, description='GMean Reviews Resonance time weighted by recentness'),  # NEW. We have a new time weighted resonance, so we should get the mean.
    FieldSchema(name="game_stats_first_review_recorded", dtype=DataType.VARCHAR, description='Date First Review Recorded', max_length=256),
    FieldSchema(name="game_stats_last_review_recorded", dtype=DataType.VARCHAR, description='Date Most Recent Review Recorded', max_length=256),

    # All category tag bool fields
    FieldSchema(name="category_tag_multiplayer", dtype=DataType.BOOL, description='Multi-player'),
    FieldSchema(name="category_tag_singleplayer", dtype=DataType.BOOL, description='Single-player'),
    FieldSchema(name="category_tag_coop", dtype=DataType.BOOL, description='Co-op'),
    FieldSchema(name="category_tag_captions", dtype=DataType.BOOL, description='Captions available'),
    FieldSchema(name="category_tag_partial_controller_support", dtype=DataType.BOOL, description='Partial Controller Support'),
    FieldSchema(name="category_tag_mods", dtype=DataType.BOOL, description='Mods'),
    FieldSchema(name="category_tag_mmo", dtype=DataType.BOOL, description='MMO'),
    FieldSchema(name="category_tag_steam_achievements", dtype=DataType.BOOL, description='Steam Achievements'),
    FieldSchema(name="category_tag_steam_cloud", dtype=DataType.BOOL, description='Steam Cloud'),
    FieldSchema(name="category_tag_shared_split_screen", dtype=DataType.BOOL, description='Shared/Split Screen'),
    FieldSchema(name="category_tag_cross_platform_multiplayer", dtype=DataType.BOOL, description='Cross-Platform Multiplayer'),
    FieldSchema(name="category_tag_full_controller_support", dtype=DataType.BOOL, description='Full controller support'),
    FieldSchema(name="category_tag_steam_workshop", dtype=DataType.BOOL, description='Steam Workshop'),
    FieldSchema(name="category_tag_vr_support", dtype=DataType.BOOL, description='VR Support'),
    FieldSchema(name="category_tag_native_steam_controller_support", dtype=DataType.BOOL, description='Native Steam Controller Support'),
    FieldSchema(name="category_tag_in_app_purchases", dtype=DataType.BOOL, description='In-App Purchases'),
    FieldSchema(name="category_tag_online_pvp", dtype=DataType.BOOL, description='Online PvP'),
    FieldSchema(name="category_tag_shared_split_screen_pvp", dtype=DataType.BOOL, description='Shared/Split Screen PvP'),
    FieldSchema(name="category_tag_online_coop", dtype=DataType.BOOL, description='Online Co-op'),
    FieldSchema(name="category_tag_shared_split_screen_coop", dtype=DataType.BOOL, description='Shared/Split Screen Co-op'),
    FieldSchema(name="category_tag_remote_play_on_phone", dtype=DataType.BOOL, description='Remote Play on Phone'),
    FieldSchema(name="category_tag_remote_play_on_tablet", dtype=DataType.BOOL, description='Remote Play on Tablet'),
    FieldSchema(name="category_tag_remote_play_on_tv", dtype=DataType.BOOL, description='Remote Play on TV'),
    FieldSchema(name="category_tag_remote_play_together", dtype=DataType.BOOL, description='Remote Play Together'),
    FieldSchema(name="category_tag_lan_pvp", dtype=DataType.BOOL, description='LAN PvP'),
    FieldSchema(name="category_tag_lan_coop", dtype=DataType.BOOL, description='LAN Co-op'),
    FieldSchema(name="category_tag_pvp", dtype=DataType.BOOL, description='PvP'),
    FieldSchema(name="category_tag_tracked_controller_support", dtype=DataType.BOOL, description='Tracked Controller Support'),
    FieldSchema(name="category_tag_vr_supported", dtype=DataType.BOOL, description='VR Supported'),
    FieldSchema(name="category_tag_vr_only", dtype=DataType.BOOL, description='VR Only'),
]

game_schema = CollectionSchema(fields=game_fields, description = "Steam Game Metadata")
game_collection = Collection(name=GAME_COLLECTION_NAME, schema=game_schema)

# Create an index for the collection.
game_index_params = {
    'index_type': 'AUTOINDEX',
    'metric_type': 'L2',
    'params': {}
}

game_collection.create_index(field_name="embedding", index_params=game_index_params)
```
```
Status(code=0, message=)
```

```python
def get_bulk_review_stats(processed_reviews):

    # Simple properties we collect.
    positivity_rating = len(processed_reviews[processed_reviews['voted_up'] == True]) / len(processed_reviews)      # NEW: 0 to 1 value of "legit" positivity rate

    # The mathier bulk properties we collect.
    geometric_mean_word_count = gmean(processed_reviews['word_count'].clip(lower=1))
    geometric_mean_unique_word_count = gmean(processed_reviews['unique_word_count'].clip(lower=1))
    geometric_mean_resonance_score = gmean(processed_reviews['resonance_score'].clip(lower=1))
    geometric_mean_time_weighted_resonance = gmean(processed_reviews['time_weighted_resonance'].clip(lower=1))      # NEW. This is the main score field for reviews. Gentle time weighting applied to resonance score.
    geometric_mean_hours_played = gmean(processed_reviews['author.playtime_forever'].clip(lower=1))
    geometric_mean_num_games_owned = gmean(processed_reviews['author.num_games_owned'].clip(lower=1))
    geometric_mean_author_num_reviews = gmean(processed_reviews['author.num_reviews'].clip(lower=1))
    
    # The date properties we collect.
    first_review_date = processed_reviews.loc[:, 'datetime_timestamp_created'].min().date()
    last_review_date = processed_reviews.loc[:, 'datetime_timestamp_created'].max().date()
    inferred_release_year = first_review_date.year

    return {
        'num_legit_reviews': len(processed_reviews),
        'positivity_rating': positivity_rating,
        'geometric_mean_word_count': geometric_mean_word_count,
        'geometric_mean_unique_word_count': geometric_mean_unique_word_count,
        'geometric_mean_resonance_score': geometric_mean_resonance_score,
        'geometric_mean_time_weighted_resonance': geometric_mean_time_weighted_resonance,                           # NEW. This is the main score field for reviews. Gentle time weighting applied to resonance score.
        'geometric_mean_hours_played': geometric_mean_hours_played,
        'geometric_mean_num_games_owned': geometric_mean_num_games_owned,
        'geometric_mean_author_num_reviews': geometric_mean_author_num_reviews,
        'first_review_date': first_review_date.strftime('%Y-%m-%d'),
        'last_review_date': last_review_date.strftime('%Y-%m-%d'),
        'inferred_release_year': int(inferred_release_year)
    }
```

```python
appids_uploaded = set()
total_appids = len(filtered_metadata_with_embeddings)

for index, row_entry in filtered_metadata_with_embeddings.iterrows():
    appid = row_entry['appid']
    print(f"Processing appid {appid} ({index+1}/{total_appids})")

    if appid in appids_uploaded:
        print(f"Skipping duplicate appid {appid}")
        continue

    game_embedding = row_entry['embedding']
    game_title = str(row_entry['name'])
    game_developers = str(row_entry['developers'])
    game_publishers = str(row_entry['publishers'])
    game_short_description = preprocess_text(row_entry['short_description'])
    game_detailed_description = preprocess_text(row_entry['detailed_description'])
    game_about_the_game = preprocess_text(row_entry['about_the_game'])
    game_supported_languages = preprocess_text(row_entry['supported_languages'])
    game_related_appids = str(row_entry['nearest_neighbors'])

    # Get the bulk review stats
    processed_reviews = filtered_reviews[filtered_reviews['appid'] == appid]
    bulk_review_stats = get_bulk_review_stats(processed_reviews)

    game_legit_review_count = np.int32(bulk_review_stats['num_legit_reviews'])
    game_positivity_rating = np.float32(bulk_review_stats['positivity_rating'])
    game_stats_gmean_word_count = np.float32(bulk_review_stats['geometric_mean_word_count'])
    game_stats_gmean_unique_word_count = np.float32(bulk_review_stats['geometric_mean_unique_word_count'])
    game_stats_gmean_hours_played = np.float32(bulk_review_stats['geometric_mean_hours_played'])
    game_stats_gmean_num_games_owned = np.float32(bulk_review_stats['geometric_mean_num_games_owned'])
    game_stats_gmean_author_num_reviews = np.float32(bulk_review_stats['geometric_mean_author_num_reviews'])
    game_stats_gmean_resonance = np.float32(bulk_review_stats['geometric_mean_resonance_score'])
    game_stats_gmean_time_weighted_resonance = np.float32(bulk_review_stats['geometric_mean_time_weighted_resonance'])
    game_stats_first_review_recorded = str(bulk_review_stats['first_review_date'])
    game_stats_last_review_recorded = str(bulk_review_stats['last_review_date'])
    game_stats_inferred_release_year = np.int32(bulk_review_stats['inferred_release_year'])

    # Get the category tags for the game
    game_category_bools = category_tags_to_bools(row_entry, category_tag_table=category_tag_table)
    category_tag_multiplayer = game_category_bools['Multi-player']
    category_tag_singleplayer = game_category_bools['Single-player']
    category_tag_coop = game_category_bools['Co-op']
    category_tag_captions = game_category_bools['Captions available']
    category_tag_partial_controller_support = game_category_bools['Partial Controller Support']
    category_tag_mods = game_category_bools['Mods']
    category_tag_mmo = game_category_bools['MMO']
    category_tag_steam_achievements = game_category_bools['Steam Achievements']
    category_tag_steam_cloud = game_category_bools['Steam Cloud']
    category_tag_shared_split_screen = game_category_bools['Shared/Split Screen']
    category_tag_cross_platform_multiplayer = game_category_bools['Cross-Platform Multiplayer']
    category_tag_full_controller_support = game_category_bools['Full controller support']
    category_tag_steam_workshop = game_category_bools['Steam Workshop']
    category_tag_vr_support = game_category_bools['VR Support']
    category_tag_native_steam_controller_support = game_category_bools['Native Steam Controller Support']
    category_tag_in_app_purchases = game_category_bools['In-App Purchases']
    category_tag_online_pvp = game_category_bools['Online PvP']
    category_tag_shared_split_screen_pvp = game_category_bools['Shared/Split Screen PvP']
    category_tag_online_coop = game_category_bools['Online Co-op']
    category_tag_shared_split_screen_coop = game_category_bools['Shared/Split Screen Co-op']
    category_tag_remote_play_on_phone = game_category_bools['Remote Play on Phone']
    category_tag_remote_play_on_tablet = game_category_bools['Remote Play on Tablet']
    category_tag_remote_play_on_tv = game_category_bools['Remote Play on TV']
    category_tag_remote_play_together = game_category_bools['Remote Play Together']
    category_tag_lan_pvp = game_category_bools['LAN PvP']
    category_tag_lan_coop = game_category_bools['LAN Co-op']
    category_tag_pvp = game_category_bools['PvP']
    category_tag_tracked_controller_support = game_category_bools['Tracked Controller Support']
    category_tag_vr_supported = game_category_bools['VR Supported']
    category_tag_vr_only = game_category_bools['VR Only']

    upload_df = pd.DataFrame({  'appid': [appid],
                                'embedding': [game_embedding],
                                'game_title': [game_title],
                                'inferred_release_year': [game_stats_inferred_release_year],
                                'game_developers': [game_developers],
                                'game_publishers': [game_publishers],
                                'game_short_description': [game_short_description],
                                'related_appids': [game_related_appids],
                                'game_detailed_description': [game_detailed_description],
                                'about_the_game': [game_about_the_game],
                                'game_supported_languages': [game_supported_languages],
                              
                                'legit_review_count': [game_legit_review_count],
                                'legit_review_positivity': [game_positivity_rating],
                                'game_stats_gmean_word_count': [game_stats_gmean_word_count],
                                'game_stats_gmean_unique_word_count': [game_stats_gmean_unique_word_count],
                                'game_stats_gmean_hours_played': [game_stats_gmean_hours_played],
                                'game_stats_gmean_author_num_reviews': [game_stats_gmean_author_num_reviews],
                                'game_stats_gmean_num_games_owned': [game_stats_gmean_num_games_owned],
                                'game_stats_gmean_resonance': [game_stats_gmean_resonance],
                                'game_stats_gmean_time_weighted_resonance': [game_stats_gmean_time_weighted_resonance],
                                'game_stats_first_review_recorded': [game_stats_first_review_recorded],
                                'game_stats_last_review_recorded': [game_stats_last_review_recorded],

                                'category_tag_multiplayer': [category_tag_multiplayer],
                                'category_tag_singleplayer': [category_tag_singleplayer],
                                'category_tag_coop': [category_tag_coop],
                                'category_tag_captions': [category_tag_captions],
                                'category_tag_partial_controller_support': [category_tag_partial_controller_support],
                                'category_tag_mods': [category_tag_mods],
                                'category_tag_mmo': [category_tag_mmo],
                                'category_tag_steam_achievements': [category_tag_steam_achievements],
                                'category_tag_steam_cloud': [category_tag_steam_cloud],
                                'category_tag_shared_split_screen': [category_tag_shared_split_screen],
                                'category_tag_cross_platform_multiplayer': [category_tag_cross_platform_multiplayer],
                                'category_tag_full_controller_support': [category_tag_full_controller_support],
                                'category_tag_steam_workshop': [category_tag_steam_workshop],
                                'category_tag_vr_support': [category_tag_vr_support],
                                'category_tag_native_steam_controller_support': [category_tag_native_steam_controller_support],
                                'category_tag_in_app_purchases': [category_tag_in_app_purchases],
                                'category_tag_online_pvp': [category_tag_online_pvp],
                                'category_tag_shared_split_screen_pvp': [category_tag_shared_split_screen_pvp],
                                'category_tag_online_coop': [category_tag_online_coop],
                                'category_tag_shared_split_screen_coop': [category_tag_shared_split_screen_coop],
                                'category_tag_remote_play_on_phone': [category_tag_remote_play_on_phone],
                                'category_tag_remote_play_on_tablet': [category_tag_remote_play_on_tablet],
                                'category_tag_remote_play_on_tv': [category_tag_remote_play_on_tv],
                                'category_tag_remote_play_together': [category_tag_remote_play_together],
                                'category_tag_lan_pvp': [category_tag_lan_pvp],
                                'category_tag_lan_coop': [category_tag_lan_coop],
                                'category_tag_pvp': [category_tag_pvp],
                                'category_tag_tracked_controller_support': [category_tag_tracked_controller_support],
                                'category_tag_vr_supported': [category_tag_vr_supported],
                                'category_tag_vr_only': [category_tag_vr_only]})


    try:
        game_collection.insert(upload_df)
        appids_uploaded.add(appid)
    except Exception as e:
        print(f'Error inserting {appid}: {e}')
        continue

    if index % 100 == 0:
        print(f'Completed {index} of {total_appids}')
        game_collection.flush() # flush to disk every 100 games
```
```
...
Processing appid 995240 (21936/21965)
Processing appid 995450 (21937/21965)
Processing appid 995460 (21938/21965)
Processing appid 995470 (21939/21965)
Processing appid 995660 (21940/21965)
Processing appid 995980 (21941/21965)
Processing appid 996080 (21942/21965)
Processing appid 996380 (21943/21965)
Processing appid 996580 (21944/21965)
Processing appid 996770 (21945/21965)
Processing appid 99700 (21946/21965)
Processing appid 997010 (21947/21965)
Processing appid 997070 (21948/21965)
Processing appid 997380 (21949/21965)
Processing appid 997700 (21950/21965)
Processing appid 997720 (21951/21965)
Processing appid 9980 (21952/21965)
Processing appid 998220 (21953/21965)
Processing appid 998740 (21954/21965)
Processing appid 998790 (21955/21965)
Processing appid 998930 (21956/21965)
Processing appid 998990 (21957/21965)
Processing appid 99900 (21958/21965)
Processing appid 999020 (21959/21965)
Processing appid 999190 (21960/21965)
Processing appid 999220 (21961/21965)
Processing appid 999410 (21962/21965)
Processing appid 999640 (21963/21965)
Processing appid 999660 (21964/21965)
Processing appid 999860 (21965/21965)
```

#### Upload GAMES and REVIEWS Collections to Paid Zilliz Instance

```python
connections.disconnect(alias='default')
```

```python
# Connect to Zilliz Cloud
use_token= 'db_admin:' + PASSWORD
connections.connect(uri=ZILLIZ_PAID_URI, username=USER, token=use_token, secure=True)
```

```python
##### !!!! OPTIONAL !!!! #####
##### THIS CODE DELETES EXISTING COLLECTIONS SO YOU CAN UPLOAD NEW ONES #####
# Remove collection if it already exists
if utility.has_collection(GAME_COLLECTION_NAME):
    utility.drop_collection(GAME_COLLECTION_NAME)
```

```python
game_fields = [
    FieldSchema(name="appid", dtype=DataType.INT64, is_primary=True, description='Steam AppID', auto_id=False),
    FieldSchema(name="embedding", dtype=DataType.FLOAT_VECTOR, description="Weighted embedding of reviews and game title, developers, publishers, short description, and about the game", dim=DIMENSION),
    FieldSchema(name="game_title", dtype=DataType.VARCHAR, description='Game Title', max_length=4096),
    FieldSchema(name="inferred_release_year", dtype=DataType.INT32, description='Inferred Release Year'),
    FieldSchema(name="game_developers", dtype=DataType.VARCHAR, description='Game Developers', max_length=4096),
    FieldSchema(name="game_publishers", dtype=DataType.VARCHAR, description='Game Publishers', max_length=4096),
    FieldSchema(name="game_short_description", dtype=DataType.VARCHAR, description='Short Description', max_length=20000),
    FieldSchema(name="related_appids", dtype=DataType.VARCHAR, description='Most similar games by Euclidean distance', max_length=1024),                    # NEW. This is the list of related appids, as determined by Euclidean distance; appids as strings separated by a | spacer.
    FieldSchema(name="game_detailed_description", dtype=DataType.VARCHAR, description='Detailed Description', max_length=65535),                            # NOTE: Turns out the detailed_description field in Steam metadata is often populated with Discord invites and merchandising. Try about_the_game instead.
    FieldSchema(name="about_the_game", dtype=DataType.VARCHAR, description='About The Game', max_length=65535),                                             # NEW. This is more frequently actually describing the game than the detailed_description field.
    #FieldSchema(name="game_controller_support", dtype=DataType.VARCHAR, description='Controller Support', max_length=512),                                 # REMOVED. This has become part of the category bools. 
    FieldSchema(name="game_supported_languages", dtype=DataType.VARCHAR, description='Supported Languages', max_length=4096),
    #FieldSchema(name="game_total_review_count", dtype=DataType.INT32, description='Total Review Count'),                                                   # RENAMED. This is now legit_review_count for further clarity.
    FieldSchema(name="legit_review_count", dtype=DataType.INT32, description='Number of reviews passing deduplication and word count filters'),             # NEW. This is the renamed replacement for total_review_count. It implies that discrepancy between our value and Steam's value is expected and normal.
    FieldSchema(name="legit_review_positivity", dtype=DataType.FLOAT, description='0 to 1 positivity score of legit reviews'),                              # NEW. This is the average positivity score of legit reviews and is a stand-in for the game's overall positivity score.
    FieldSchema(name="game_stats_gmean_word_count", dtype=DataType.FLOAT, description='GMean Reviews Word Count'),
    FieldSchema(name="game_stats_gmean_unique_word_count", dtype=DataType.FLOAT, description='GMean Reviews Unique Word Count'),
    FieldSchema(name="game_stats_gmean_hours_played", dtype=DataType.FLOAT, description='GMean Reviews Hours Played'),
    FieldSchema(name="game_stats_gmean_author_num_reviews", dtype=DataType.FLOAT, description='GMean Reviews Author Num Reviews'),
    FieldSchema(name="game_stats_gmean_num_games_owned", dtype=DataType.FLOAT, description='GMean Reviews Author Num Games Owned'),                         # NEW. This is the average number of games owned by the authors of the reviews.
    FieldSchema(name="game_stats_gmean_resonance", dtype=DataType.FLOAT, description='GMean Reviews Resonance'),                                            # NOTE: If you want to use a timeless measurement, use this one.
    FieldSchema(name="game_stats_gmean_time_weighted_resonance", dtype=DataType.FLOAT, description='GMean Reviews Resonance time weighted by recentness'),  # NEW. We have a new time weighted resonance, so we should get the mean.
    FieldSchema(name="game_stats_first_review_recorded", dtype=DataType.VARCHAR, description='Date First Review Recorded', max_length=256),
    FieldSchema(name="game_stats_last_review_recorded", dtype=DataType.VARCHAR, description='Date Most Recent Review Recorded', max_length=256),

    # All category tag bool fields
    FieldSchema(name="category_tag_multiplayer", dtype=DataType.BOOL, description='Multi-player'),
    FieldSchema(name="category_tag_singleplayer", dtype=DataType.BOOL, description='Single-player'),
    FieldSchema(name="category_tag_coop", dtype=DataType.BOOL, description='Co-op'),
    FieldSchema(name="category_tag_captions", dtype=DataType.BOOL, description='Captions available'),
    FieldSchema(name="category_tag_partial_controller_support", dtype=DataType.BOOL, description='Partial Controller Support'),
    FieldSchema(name="category_tag_mods", dtype=DataType.BOOL, description='Mods'),
    FieldSchema(name="category_tag_mmo", dtype=DataType.BOOL, description='MMO'),
    FieldSchema(name="category_tag_steam_achievements", dtype=DataType.BOOL, description='Steam Achievements'),
    FieldSchema(name="category_tag_steam_cloud", dtype=DataType.BOOL, description='Steam Cloud'),
    FieldSchema(name="category_tag_shared_split_screen", dtype=DataType.BOOL, description='Shared/Split Screen'),
    FieldSchema(name="category_tag_cross_platform_multiplayer", dtype=DataType.BOOL, description='Cross-Platform Multiplayer'),
    FieldSchema(name="category_tag_full_controller_support", dtype=DataType.BOOL, description='Full controller support'),
    FieldSchema(name="category_tag_steam_workshop", dtype=DataType.BOOL, description='Steam Workshop'),
    FieldSchema(name="category_tag_vr_support", dtype=DataType.BOOL, description='VR Support'),
    FieldSchema(name="category_tag_native_steam_controller_support", dtype=DataType.BOOL, description='Native Steam Controller Support'),
    FieldSchema(name="category_tag_in_app_purchases", dtype=DataType.BOOL, description='In-App Purchases'),
    FieldSchema(name="category_tag_online_pvp", dtype=DataType.BOOL, description='Online PvP'),
    FieldSchema(name="category_tag_shared_split_screen_pvp", dtype=DataType.BOOL, description='Shared/Split Screen PvP'),
    FieldSchema(name="category_tag_online_coop", dtype=DataType.BOOL, description='Online Co-op'),
    FieldSchema(name="category_tag_shared_split_screen_coop", dtype=DataType.BOOL, description='Shared/Split Screen Co-op'),
    FieldSchema(name="category_tag_remote_play_on_phone", dtype=DataType.BOOL, description='Remote Play on Phone'),
    FieldSchema(name="category_tag_remote_play_on_tablet", dtype=DataType.BOOL, description='Remote Play on Tablet'),
    FieldSchema(name="category_tag_remote_play_on_tv", dtype=DataType.BOOL, description='Remote Play on TV'),
    FieldSchema(name="category_tag_remote_play_together", dtype=DataType.BOOL, description='Remote Play Together'),
    FieldSchema(name="category_tag_lan_pvp", dtype=DataType.BOOL, description='LAN PvP'),
    FieldSchema(name="category_tag_lan_coop", dtype=DataType.BOOL, description='LAN Co-op'),
    FieldSchema(name="category_tag_pvp", dtype=DataType.BOOL, description='PvP'),
    FieldSchema(name="category_tag_tracked_controller_support", dtype=DataType.BOOL, description='Tracked Controller Support'),
    FieldSchema(name="category_tag_vr_supported", dtype=DataType.BOOL, description='VR Supported'),
    FieldSchema(name="category_tag_vr_only", dtype=DataType.BOOL, description='VR Only'),
]

game_schema = CollectionSchema(fields=game_fields, description = "Steam Game Metadata")
game_collection = Collection(name=GAME_COLLECTION_NAME, schema=game_schema)

# Create an index for the collection.
game_index_params = {
    'index_type': 'AUTOINDEX',
    'metric_type': 'L2',
    'params': {}
}

game_collection.create_index(field_name="embedding", index_params=game_index_params)
```
```
Status(code=0, message=)
```

```python
appids_uploaded = set()
total_appids = len(filtered_metadata_with_embeddings)

for index, row_entry in filtered_metadata_with_embeddings.iterrows():
    appid = row_entry['appid']
    print(f"Processing appid {appid} ({index+1}/{total_appids})")

    if appid in appids_uploaded:
        print(f"Skipping duplicate appid {appid}")
        continue

    game_embedding = row_entry['embedding']
    game_title = str(row_entry['name'])
    game_developers = str(row_entry['developers'])
    game_publishers = str(row_entry['publishers'])
    game_short_description = preprocess_text(row_entry['short_description'])
    game_detailed_description = preprocess_text(row_entry['detailed_description'])
    game_about_the_game = preprocess_text(row_entry['about_the_game'])
    game_supported_languages = preprocess_text(row_entry['supported_languages'])
    game_related_appids = str(row_entry['nearest_neighbors'])

    # Get the bulk review stats
    processed_reviews = filtered_reviews[filtered_reviews['appid'] == appid]
    bulk_review_stats = get_bulk_review_stats(processed_reviews)

    game_legit_review_count = np.int32(bulk_review_stats['num_legit_reviews'])
    game_positivity_rating = np.float32(bulk_review_stats['positivity_rating'])
    game_stats_gmean_word_count = np.float32(bulk_review_stats['geometric_mean_word_count'])
    game_stats_gmean_unique_word_count = np.float32(bulk_review_stats['geometric_mean_unique_word_count'])
    game_stats_gmean_hours_played = np.float32(bulk_review_stats['geometric_mean_hours_played'])
    game_stats_gmean_num_games_owned = np.float32(bulk_review_stats['geometric_mean_num_games_owned'])
    game_stats_gmean_author_num_reviews = np.float32(bulk_review_stats['geometric_mean_author_num_reviews'])
    game_stats_gmean_resonance = np.float32(bulk_review_stats['geometric_mean_resonance_score'])
    game_stats_gmean_time_weighted_resonance = np.float32(bulk_review_stats['geometric_mean_time_weighted_resonance'])
    game_stats_first_review_recorded = str(bulk_review_stats['first_review_date'])
    game_stats_last_review_recorded = str(bulk_review_stats['last_review_date'])
    game_stats_inferred_release_year = np.int32(bulk_review_stats['inferred_release_year'])

    # Get the category tags for the game
    game_category_bools = category_tags_to_bools(row_entry, category_tag_table=category_tag_table)
    category_tag_multiplayer = game_category_bools['Multi-player']
    category_tag_singleplayer = game_category_bools['Single-player']
    category_tag_coop = game_category_bools['Co-op']
    category_tag_captions = game_category_bools['Captions available']
    category_tag_partial_controller_support = game_category_bools['Partial Controller Support']
    category_tag_mods = game_category_bools['Mods']
    category_tag_mmo = game_category_bools['MMO']
    category_tag_steam_achievements = game_category_bools['Steam Achievements']
    category_tag_steam_cloud = game_category_bools['Steam Cloud']
    category_tag_shared_split_screen = game_category_bools['Shared/Split Screen']
    category_tag_cross_platform_multiplayer = game_category_bools['Cross-Platform Multiplayer']
    category_tag_full_controller_support = game_category_bools['Full controller support']
    category_tag_steam_workshop = game_category_bools['Steam Workshop']
    category_tag_vr_support = game_category_bools['VR Support']
    category_tag_native_steam_controller_support = game_category_bools['Native Steam Controller Support']
    category_tag_in_app_purchases = game_category_bools['In-App Purchases']
    category_tag_online_pvp = game_category_bools['Online PvP']
    category_tag_shared_split_screen_pvp = game_category_bools['Shared/Split Screen PvP']
    category_tag_online_coop = game_category_bools['Online Co-op']
    category_tag_shared_split_screen_coop = game_category_bools['Shared/Split Screen Co-op']
    category_tag_remote_play_on_phone = game_category_bools['Remote Play on Phone']
    category_tag_remote_play_on_tablet = game_category_bools['Remote Play on Tablet']
    category_tag_remote_play_on_tv = game_category_bools['Remote Play on TV']
    category_tag_remote_play_together = game_category_bools['Remote Play Together']
    category_tag_lan_pvp = game_category_bools['LAN PvP']
    category_tag_lan_coop = game_category_bools['LAN Co-op']
    category_tag_pvp = game_category_bools['PvP']
    category_tag_tracked_controller_support = game_category_bools['Tracked Controller Support']
    category_tag_vr_supported = game_category_bools['VR Supported']
    category_tag_vr_only = game_category_bools['VR Only']

    upload_df = pd.DataFrame({  'appid': [appid],
                                'embedding': [game_embedding],
                                'game_title': [game_title],
                                'inferred_release_year': [game_stats_inferred_release_year],
                                'game_developers': [game_developers],
                                'game_publishers': [game_publishers],
                                'game_short_description': [game_short_description],
                                'related_appids': [game_related_appids],
                                'game_detailed_description': [game_detailed_description],
                                'about_the_game': [game_about_the_game],
                                'game_supported_languages': [game_supported_languages],
                              
                                'legit_review_count': [game_legit_review_count],
                                'legit_review_positivity': [game_positivity_rating],
                                'game_stats_gmean_word_count': [game_stats_gmean_word_count],
                                'game_stats_gmean_unique_word_count': [game_stats_gmean_unique_word_count],
                                'game_stats_gmean_hours_played': [game_stats_gmean_hours_played],
                                'game_stats_gmean_author_num_reviews': [game_stats_gmean_author_num_reviews],
                                'game_stats_gmean_num_games_owned': [game_stats_gmean_num_games_owned],
                                'game_stats_gmean_resonance': [game_stats_gmean_resonance],
                                'game_stats_gmean_time_weighted_resonance': [game_stats_gmean_time_weighted_resonance],
                                'game_stats_first_review_recorded': [game_stats_first_review_recorded],
                                'game_stats_last_review_recorded': [game_stats_last_review_recorded],

                                'category_tag_multiplayer': [category_tag_multiplayer],
                                'category_tag_singleplayer': [category_tag_singleplayer],
                                'category_tag_coop': [category_tag_coop],
                                'category_tag_captions': [category_tag_captions],
                                'category_tag_partial_controller_support': [category_tag_partial_controller_support],
                                'category_tag_mods': [category_tag_mods],
                                'category_tag_mmo': [category_tag_mmo],
                                'category_tag_steam_achievements': [category_tag_steam_achievements],
                                'category_tag_steam_cloud': [category_tag_steam_cloud],
                                'category_tag_shared_split_screen': [category_tag_shared_split_screen],
                                'category_tag_cross_platform_multiplayer': [category_tag_cross_platform_multiplayer],
                                'category_tag_full_controller_support': [category_tag_full_controller_support],
                                'category_tag_steam_workshop': [category_tag_steam_workshop],
                                'category_tag_vr_support': [category_tag_vr_support],
                                'category_tag_native_steam_controller_support': [category_tag_native_steam_controller_support],
                                'category_tag_in_app_purchases': [category_tag_in_app_purchases],
                                'category_tag_online_pvp': [category_tag_online_pvp],
                                'category_tag_shared_split_screen_pvp': [category_tag_shared_split_screen_pvp],
                                'category_tag_online_coop': [category_tag_online_coop],
                                'category_tag_shared_split_screen_coop': [category_tag_shared_split_screen_coop],
                                'category_tag_remote_play_on_phone': [category_tag_remote_play_on_phone],
                                'category_tag_remote_play_on_tablet': [category_tag_remote_play_on_tablet],
                                'category_tag_remote_play_on_tv': [category_tag_remote_play_on_tv],
                                'category_tag_remote_play_together': [category_tag_remote_play_together],
                                'category_tag_lan_pvp': [category_tag_lan_pvp],
                                'category_tag_lan_coop': [category_tag_lan_coop],
                                'category_tag_pvp': [category_tag_pvp],
                                'category_tag_tracked_controller_support': [category_tag_tracked_controller_support],
                                'category_tag_vr_supported': [category_tag_vr_supported],
                                'category_tag_vr_only': [category_tag_vr_only]})


    try:
        game_collection.insert(upload_df)
        appids_uploaded.add(appid)
    except Exception as e:
        print(f'Error inserting {appid}: {e}')
        continue

    if index % 100 == 0:
        print(f'Completed {index} of {total_appids}')
        game_collection.flush() # flush to disk every 100 games
```
```
...
Processing appid 995240 (21936/21965)
Processing appid 995450 (21937/21965)
Processing appid 995460 (21938/21965)
Processing appid 995470 (21939/21965)
Processing appid 995660 (21940/21965)
Processing appid 995980 (21941/21965)
Processing appid 996080 (21942/21965)
Processing appid 996380 (21943/21965)
Processing appid 996580 (21944/21965)
Processing appid 996770 (21945/21965)
Processing appid 99700 (21946/21965)
Processing appid 997010 (21947/21965)
Processing appid 997070 (21948/21965)
Processing appid 997380 (21949/21965)
Processing appid 997700 (21950/21965)
Processing appid 997720 (21951/21965)
Processing appid 9980 (21952/21965)
Processing appid 998220 (21953/21965)
Processing appid 998740 (21954/21965)
Processing appid 998790 (21955/21965)
Processing appid 998930 (21956/21965)
Processing appid 998990 (21957/21965)
Processing appid 99900 (21958/21965)
Processing appid 999020 (21959/21965)
Processing appid 999190 (21960/21965)
Processing appid 999220 (21961/21965)
Processing appid 999410 (21962/21965)
Processing appid 999640 (21963/21965)
Processing appid 999660 (21964/21965)
Processing appid 999860 (21965/21965)
```

```python
# ##### !!!! OPTIONAL !!!! #####
# ##### THIS CODE DELETES EXISTING COLLECTIONS SO YOU CAN UPLOAD NEW ONES #####
# # Remove collection if it already exists
if utility.has_collection(REVIEW_COLLECTION_NAME):
    utility.drop_collection(REVIEW_COLLECTION_NAME)
```

```python
review_fields = [
    FieldSchema(name="review_id", dtype=DataType.INT64, is_primary=True, description='Steam Review ID', auto_id=False),
    FieldSchema(name="embedding", dtype=DataType.FLOAT_VECTOR, description="Embedding of processed review text", dim=DIMENSION),
    FieldSchema(name="appid", dtype=DataType.INT64, description='Game Steam AppID'),
    FieldSchema(name='recommended', dtype=DataType.BOOL, description='Recommended, otherwise called voted_up'),                         # NEW: Did the user give the game a thumbs up or thumbs down?
    FieldSchema(name='language', dtype=DataType.VARCHAR, description='Language Region of the review', max_length=256),                  # NEW: Language of the review. NOTE: THESE ARE REGIONAL (i.e. 'Brazilian' is listed as a language)
    FieldSchema(name="review", dtype=DataType.VARCHAR, description='Review Text', max_length=65535),
    FieldSchema(name="playtime_forever", dtype=DataType.INT32, description='Playtime Forever'),
    FieldSchema(name="review_date", dtype=DataType.VARCHAR, description='Review Date', max_length=256),
    FieldSchema(name="resonance_score", dtype=DataType.FLOAT, description='Resonance Score'),                                           # NOTE: If you want a timeless measurement, use this one.
    FieldSchema(name="time_weighted_resonance_score", dtype=DataType.FLOAT, description='Resonance Score')                              # NEW: Main score field for reviews. Gentle time weighting applied to resonance score. 
]

review_schema = CollectionSchema(fields=review_fields, description = "Steam Review Metadata")
review_collection = Collection(name=REVIEW_COLLECTION_NAME, schema=review_schema)

# Create an index for the collection.
review_index_params = {
    'index_type': 'AUTOINDEX',
    'metric_type': 'L2',
    'params': {}
}

review_collection.create_index(field_name="embedding", index_params=review_index_params)
```
```
Status(code=0, message=)
```

```python
review_appids_uploaded = set()
unique_review_appids = filtered_reviews['appid'].unique()
cur_index = -1

for appid in unique_review_appids:
    cur_index += 1

    print(f'Processing {appid} ({cur_index} of {len(unique_review_appids)})')

    if appid in review_appids_uploaded:
        print(f'Already uploaded {appid}')
        continue

    if not os.path.exists(os.path.join(review_embeddings_output_folder, f'{appid}.pickle')):
        print(f'No embeddings found for {appid}')
        continue

    review_entries = filtered_reviews[filtered_reviews['appid'] == appid]

    # Load the corresponding pickle file
    review_embeddings = pd.read_pickle(os.path.join(review_embeddings_output_folder, f'{appid}.pickle'))

    # Convert the embeddings list to a DataFrame
    recommendationids = [item[0] for item in review_embeddings]
    embeddings_list = [item[1] for item in review_embeddings]
    embeddings_df = pd.DataFrame({'recommendationid': recommendationids, 'embedding': embeddings_list})
    
    # Match each 'recommendationid' in the embeddings with its 'time_weighted_resonance' in group
    merged = pd.merge(review_entries, embeddings_df, on='recommendationid')
    
    reviews_df_perfectly_formatted = pd.DataFrame({'review_id': merged['recommendationid'],
                                'embedding': merged['embedding'],
                                'appid': merged['appid'],
                                'recommended': merged['voted_up'],
                                'language': merged['language'],
                                'review': merged['processed_review'],
                                'playtime_forever': merged['author.playtime_forever'].astype(np.int32),
                                'review_date': merged['datetime_timestamp_updated'].astype(str),
                                'resonance_score': merged['resonance_score'].astype(np.float32),
                                'time_weighted_resonance_score': merged['time_weighted_resonance'].astype(np.float32)})

    try:
        review_collection.insert(reviews_df_perfectly_formatted)
        review_appids_uploaded.add(appid)
        review_collection.flush() # Every review batch is up to 200 objects worth of embeddings. These are hefty enough to require lots of flushing to disk
    except Exception as e:
        print(f'Error inserting {appid}: {e}')
        continue

    if cur_index % 100 == 0:
        print(f'Completed {cur_index} of {len(unique_review_appids)}')
```

```
...
Processing 994340 (21926 of 21965)
Processing 994500 (21927 of 21965)
Processing 994670 (21928 of 21965)
Processing 994730 (21929 of 21965)
Processing 994790 (21930 of 21965)
Processing 994870 (21931 of 21965)
Processing 995050 (21932 of 21965)
Processing 995070 (21933 of 21965)
Processing 995230 (21934 of 21965)
Processing 995240 (21935 of 21965)
Processing 995450 (21936 of 21965)
Processing 995460 (21937 of 21965)
Processing 995470 (21938 of 21965)
Processing 995660 (21939 of 21965)
Processing 995980 (21940 of 21965)
Processing 996080 (21941 of 21965)
Processing 996380 (21942 of 21965)
Processing 996580 (21943 of 21965)
Processing 996770 (21944 of 21965)
Processing 99700 (21945 of 21965)
Processing 997010 (21946 of 21965)
Processing 997070 (21947 of 21965)
Processing 997380 (21948 of 21965)
Processing 997700 (21949 of 21965)
Processing 997720 (21950 of 21965)
Processing 9980 (21951 of 21965)
Processing 998220 (21952 of 21965)
Processing 998740 (21953 of 21965)
Processing 998790 (21954 of 21965)
Processing 998930 (21955 of 21965)
Processing 998990 (21956 of 21965)
Processing 99900 (21957 of 21965)
Processing 999020 (21958 of 21965)
Processing 999190 (21959 of 21965)
Processing 999220 (21960 of 21965)
Processing 999410 (21961 of 21965)
Processing 999640 (21962 of 21965)
Processing 999660 (21963 of 21965)
Processing 999860 (21964 of 21965)
```