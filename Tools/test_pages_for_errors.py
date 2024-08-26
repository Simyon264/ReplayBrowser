import asyncio
import subprocess
import signal
import os
from playwright.async_api import async_playwright

async def run_tests():
    process = subprocess.Popen(
        [
            "dotnet",
            "run",
            "--no-build",
            "--project",
            "./ReplayBrowser/ReplayBrowser.csproj",
            "--configuration",
            "Testing"
        ],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        preexec_fn=os.setsid,
    )

    error_found = {"value": False}

    try:
        print("Waiting for the application to start...")
        await asyncio.sleep(20)

        async with async_playwright() as p:
            browser = await p.chromium.launch(headless=True)
            context = await browser.new_context()
            page = await context.new_page()

            urls = [
                "http://localhost:5000/",  # Home page
                "http://localhost:5000/privacy",  # Privacy page
                "http://localhost:5000/contact",  # Contact page
                "http://localhost:5000/leaderboard",  # Leaderboard page
                "http://localhost:5000/player/aac26166-139a-4163-8aa9-ad2a059a427d",  # Player page (no redaction)
                "http://localhost:5000/player/8ced134c-8731-4087-bed3-107d59af1a11",  # Player page (redacted)
                "http://localhost:5000/changelog",  # Changelog page
                "http://localhost:5000/replay/3",  # Replay page
            ]

            for url in urls:
                try:
                    print(f"Visiting {url}")
                    response = await page.goto(url, wait_until='networkidle')

                    if not response:
                        print(f"Failed to load {url}: No response received")
                        error_found["value"] = True
                        continue

                    if not response.ok:
                        print(f"Failed to load {url}: Status {response.status}")
                        error_found["value"] = True
                        continue

                    await asyncio.sleep(3)

                    await page.wait_for_selector('body', timeout=5000)

                    exception_elements = await page.query_selector_all('pre.rawExceptionStackTrace')
                    if exception_elements:
                        error_message = await exception_elements[0].text_content()
                        print(f"Error found on {url}: {error_message}")
                        error_found["value"] = True
                    else:
                        print("No errors found")

                except Exception as e:
                    print(f"Error visiting {url}: {e}")
                    error_found["value"] = True

            await browser.close()

        if error_found["value"]:
            raise Exception("Test failed due to console errors or exceptions")

    finally:
        print("Stopping the ASP.NET application...")
        try:
            os.killpg(os.getpgid(process.pid), signal.SIGTERM)
        except Exception as e:
            print(f"Error stopping the application: {e}")

if __name__ == "__main__":
    asyncio.run(run_tests())