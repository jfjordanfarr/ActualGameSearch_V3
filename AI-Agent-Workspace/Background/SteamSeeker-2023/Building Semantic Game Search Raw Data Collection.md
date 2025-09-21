Raw Data Collection

As an approved developer who is working within the constraints of the API agreement, I have procured reviews with the following two simple python scripts

## CollectMedatadata.py

```python
import argparse
import os
import json
import time
import requests
import pandas as pd
import csv
import random
from steam.webapi import WebAPI
from pandas import DataFrame, Series
from typing import Union

def get_app_metadata(appid: str, save_path: str, max_attempts: int = 30) -> Union[DataFrame, None]:
    filename = os.path.join(save_path, f"{appid}.json")

    # If the metadata already exists, load it and return
    if os.path.exists(filename):
        with open(filename, 'r') as f:
            data = json.load(f)
        return pd.json_normalize(data)

    # The metadata doesn't exist, fetch it
    url = f'https://store.steampowered.com/api/appdetails?appids={appid}'
    headers = {
        'Accept': 'application/json',
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36',
    }

    for attempt in range(max_attempts):
        try:
            response = requests.get(url=url, headers=headers)
            response.raise_for_status()
            data = response.json()

            if str(appid) in data.keys():
                data = data[str(appid)]
            else:
                return None
            if 'success' in data.keys():
                if data['success'] == False:
                    return None
                else:
                    data = data['data']
            else:
                return None

            with open(filename, 'w') as f:
                json.dump(data, f)
            time.sleep(1) # Sleep for 1 second to avoid rate limit
            return pd.json_normalize(data)
        except requests.exceptions.HTTPError as e:
            if response.status_code == 429:
                print(f"Rate limit exceeded for appid: {appid}. Retrying in 60 seconds.")
                time.sleep(60)
            else:
                print(f"HTTP error for appid: {appid}. Error: {e}. Skipping.")
                return None
        except requests.exceptions.RequestException as e:
            print(f"Request failed for appid: {appid}. Error: {e}. Retrying in 3 seconds.")
            time.sleep(3)
        except json.JSONDecodeError as e:
            print(f"Failed to parse JSON for appid: {appid}. Error: {e}. Skipping.")
            return None
        except Exception as e:
            print(f"Unexpected error occurred while parsing JSON for appid: {appid}. Error: {e}. Skipping.")
            return None

    print(f"Failed to get response for appid: {appid} after {max_attempts} attempts. Skipping.")
    return None

def main():
    parser = argparse.ArgumentParser(description='Script to collect metadata from Steam.')
    parser.add_argument('--folder', default='./Downloaded_Metadata/', help='The folder to store collected metadata')
    args = parser.parse_args()

    steam_api_key = os.environ.get("STEAM_API_KEY")
    api = WebAPI(steam_api_key, format="json", raw=False)

    rawAppsList = api.ISteamApps.GetAppList()
    currentAppsList = pd.DataFrame(rawAppsList['applist']['apps'])

    save_path = args.folder
    os.makedirs(save_path, exist_ok=True)

    # Get list of appids for which metadata doesn't exist
    appids_to_get = [str(row['appid']) for _, row in currentAppsList.iterrows() if not os.path.exists(os.path.join(save_path, f"{row['appid']}.json"))]

    # Shuffle the appids
    random.shuffle(appids_to_get)

    metadata_df = DataFrame()
    failed_apps = []

    for appid in appids_to_get:
        metadata = get_app_metadata(appid, save_path)
        if metadata is not None:
            metadata_df = pd.concat([metadata_df, metadata], ignore_index=True)
        else:
            failed_apps.append(appid)

        # Save intermediate and final results to disk
        if (appids_to_get.index(appid) + 1) % 100 == 0 or (appids_to_get.index(appid) + 1) == len(appids_to_get):
            print(f"Completed {appids_to_get.index(appid) + 1} requests. Last appid: {appid}")
            metadata_df.to_csv(os.path.join(save_path, "AllAppsMetadata.csv"), index=False, quoting=csv.QUOTE_ALL)
            with open(os.path.join(save_path, "FailedApps.txt"), 'w') as f:
                for app in failed_apps:
                    f.write(f"{app}\n")

if __name__ == "__main__":
    main()

    
```

## CollectReviews.py

```python
import argparse
import os
import csv
import requests
import pandas as pd
import time
import random
from steam.webapi import WebAPI

def collectReviews(appid, reviewsURL, max_retries=3):
    returnReviews = pd.DataFrame()
    cursor = "*"
    usedCursors = {cursor}
    maxLoopNum = 10000
    loopNum = 0
    gotAllReviews = False
    while not gotAllReviews:
        params = {
            "json": 1,
            "filter": "recent",
            "language": "all",
            "review_type": "all",
            "purchase_type": "steam",
            "num_per_page": 100,
            "cursor": cursor
        }
        retry_count = 0
        while retry_count < max_retries:
            try:
                response = requests.get(url=reviewsURL+appid, params=params, timeout=5)
                if response.status_code == 200:
                    response = response.json()
                    if not response['reviews']:
                        gotAllReviews = True # Attempted fix for getting stuck looping over games with no reviews 10k times.
                        break
                    cursor = response['cursor']
                    normalizedReviews = pd.json_normalize(response['reviews'])
                    returnReviews = pd.concat([returnReviews, normalizedReviews], ignore_index=True)

                    if cursor in usedCursors:
                        gotAllReviews = True
                    else:
                        usedCursors.add(cursor)

                    loopNum += 1
                    if loopNum == maxLoopNum:
                        gotAllReviews = True
                elif response.status_code == 429:
                    print("Rate limited. Waiting 60 seconds...")
                    time.sleep(60)
                else:
                    print(f"Error: {response.status_code}")
                    break
            except requests.exceptions.RequestException as e:
                print(f"Error: {e}")
                retry_count += 1
                continue
            break

        if retry_count == max_retries:
            print(f"Max retries exceeded for app {appid}. Skipping...")
            break

    if returnReviews.empty:
        return returnReviews

    returnReviews.drop_duplicates(subset=['recommendationid'], inplace=True)
    returnReviews.drop(columns=['timestamp_dev_responded', 'developer_response'], errors='ignore', inplace=True)

    return returnReviews

def main():
    parser = argparse.ArgumentParser(description='Script to collect reviews from Steam.')
    parser.add_argument('--key', required=True, help='Your Steam API key')
    parser.add_argument('--folder', default='./Downloaded_Reviews/', help='The folder to store collected reviews')
    args = parser.parse_args()

    steam_api_key = args.key
    api = WebAPI(steam_api_key, format="json", raw=False)

    rawAppsList = api.ISteamApps.GetAppList()
    currentAppsList = pd.DataFrame(rawAppsList['applist']['apps'])

    currentAppsList = currentAppsList[currentAppsList['name'] != '']

    reviewsURL = 'https://store.steampowered.com/appreviews/'

    processed_apps = set()
    for filename in os.listdir(args.folder):
        if filename.endswith(".csv"):
            appid = filename[:-4]
            processed_apps.add(appid)

    failed_apps = set()
    failed_apps_file = os.path.join(args.folder, "failed_apps.txt")
    if os.path.exists(failed_apps_file):
        with open(failed_apps_file, "r") as f:
            failed_apps = set(f.read().splitlines())

    # Get list of appids for which reviews don't exist
    appids_to_get = [str(row['appid']) for _, row in currentAppsList.iterrows() if str(row['appid']) not in processed_apps and str(row['appid']) not in failed_apps]

    # Shuffle the appids
    random.shuffle(appids_to_get)

    consecutive_errors = 0
    max_consecutive_errors = 10  # You can adjust this value

    for appid in appids_to_get:
        try:
            collectedReviews = collectReviews(appid, reviewsURL)
            if not collectedReviews.empty:
                collectedReviews.to_csv(os.path.join(args.folder, f"{appid}.csv"), index=False, quoting=csv.QUOTE_ALL)
                processed_apps.add(appid)
                consecutive_errors = 0  # reset the error counter when successful
            else:
                failed_apps.add(appid)
                with open(failed_apps_file, "a") as f:
                    f.write(f"{appid}\n")
        except KeyboardInterrupt:
            with open(os.path.join(args.folder, "progress.txt"), "w") as f:
                f.write(f"{appid}")
            break
        except Exception as e:
            print(f"Error: {e} for app {appid}")
            failed_apps.add(appid)
            with open(failed_apps_file, "a") as f:
                f.write(f"{appid}\n")
            consecutive_errors += 1
            if consecutive_errors >= max_consecutive_errors:
                print("Too many consecutive errors. Stopping execution.")
                break
            continue

if __name__ == "__main__":
    main()

```