# Replay Browser

This is the repository for a replay browser for Space Station 14. The replay browser downloads replays from the provided paths, parses them, and then finally inserts it into the provided DB.

## Setup

Setting up a dev env is simple.
These instructions assume that you have a postgres database set up.

1. Clone the repository.
2. Set up the appsettings file.
   Create a file named `appsettings.Secret.json` in the server project.
   This is where you can put your connection string for the postgres DB.
3. Run both the server and client using `dotnet`. The server will now download a lot of replays. This will take some time and it will use about 50 Mbps. You can keep using your computer during this time.

## Screenshots
<details>
  <summary>View</summary>

  ![image](https://github.com/Simyon264/ReplayBrowser/assets/63975668/f46c954f-cab1-4b95-be62-ee4d79329305)

![image](https://github.com/Simyon264/ReplayBrowser/assets/63975668/c1e7b857-d643-4ca2-a69d-c62f3bbc383e)

![image](https://github.com/Simyon264/ReplayBrowser/assets/63975668/3efa2506-cc35-44f3-91d5-d99cdcba7a66)

![image](https://github.com/Simyon264/ReplayBrowser/assets/63975668/9bf1753d-ca22-466b-89ab-9f4aba186666)

![image](https://github.com/Simyon264/ReplayBrowser/assets/63975668/c4b2212c-9644-448e-9458-551d6e5b6edc)
</details>
