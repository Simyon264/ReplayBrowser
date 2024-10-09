# Replay Browser

This is the repository for a replay browser for Space Station 14. The replay browser downloads replays from the provided paths, parses them, and then finally inserts it into the provided DB.
You can view the deployed website [here](https://replay.unstablefoundation.de/)

## Setup

Setting up a dev env is simple.
These instructions assume that you have a postgres database set up.

1. Clone the repository.
2. Set up the appsettings file.
   Create a file named `appsettings.Secret.json` in the server project.
   This is where you can put your connection string for the postgres DB.
   Look at the example appsettings file below.
3. Run both the server and client using `dotnet`. The server will now download a lot of replays. This will take some time and it will use about 50 Mbps. You can keep using your computer during this time.

### Example appsettings.Secret.json

```json lines
// Note: You cannot use comments in JSON files, this is just for readability.
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ReplayBrowser;Username=postgres;Password=<Password>"
  },
  "ProxyIP": "127.0.10.1",
   
   // You do not need to set these, but you must have them set if you need to test something with OAuth.
   "ClientId": "<ClientId>", // You can get this from https://account.spacestation14.com/Identity/Account/Manage/Developer
   "ClientSecret": "<ClientSecret>", // You can get this from https://account.spacestation14.com/Identity/Account/Manage/Developer
  
   "Kestrel": { // This is the port the server will run on, you can change this to whatever you want
    "EndPoints": {
      "Http": {
        "Url": "http://localhost:12500"
      },
      "Https": {
        "Url": "https://localhost:12501"
      }
    }
  },
  "Contact": { // These need to be set, but the value is never checked, so they can be anything
    "Email": "local",
    "Discord": "local",
    "Server": "local"
  },
   "Plausible": {
     // This is the domain for the plausible analytics, you can set this to your own domain if you want to track analytics
     "Snippet": "<script ...>", // This will go into the head of every page
     "Enabled": true,
   }
}
```

## Screenshots
<details>
  <summary>View</summary>

  ![image](https://github.com/Simyon264/ReplayBrowser/assets/63975668/f46c954f-cab1-4b95-be62-ee4d79329305)

![image](https://github.com/Simyon264/ReplayBrowser/assets/63975668/c1e7b857-d643-4ca2-a69d-c62f3bbc383e)

![image](https://github.com/Simyon264/ReplayBrowser/assets/63975668/3efa2506-cc35-44f3-91d5-d99cdcba7a66)

![image](https://github.com/Simyon264/ReplayBrowser/assets/63975668/9bf1753d-ca22-466b-89ab-9f4aba186666)

![image](https://github.com/Simyon264/ReplayBrowser/assets/63975668/c4b2212c-9644-448e-9458-551d6e5b6edc)
</details>
