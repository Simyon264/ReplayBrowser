import asyncio
import subprocess
import time
import signal
import os
from pyppeteer import launch

async def run_tests():
    # Start the project
    process = subprocess.Popen(
        ["dotnet", "run", "--no-build"],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        preexec_fn=os.setsid
    )

    error_found = {"value": False}

    try:
        print("Waiting for the application to start...")
        time.sleep(10)

        # Start the browser
        browser = await launch(headless=True)
        page = await browser.newPage()

        urls = [
            "https://localhost:5000/", # Home page
            "https://localhost:5000/privacy", # Privacy page
            "https://localhost:5000/contact", # Contact page
            "https://localhost:5000/leaderboard", # Leaderboard page
            "https://localhost:5000/player/aac26166-139a-4163-8aa9-ad2a059a427d", # Player page (no redaction)
            "https://localhost:5000/player/8ced134c-8731-4087-bed3-107d59af1a11", # Player page (redacted [proected, but same thing pretty much])
            "https://localhost:5000/downloads", # Downloads page
            "https://localhost:5000/changelog", # Changelog page
            "https://localhost:5000/replay/3", # Replay page
            "https://localhost:5000/replay/312321", # Replay page (non-existant replay)
            "https://localhost:5000/NOTFOUND" # non existant page
        ]

        for url in urls:
            try:
                print(f"Visiting {url}")
                page.on('console', lambda msg: handle_console_message(msg, error_found))
                await page.goto(url)
                await page.waitForSelector('body', timeout=5000)

                exception_elements = await page.querySelectorAll('pre.rawExceptionStackTrace')  # ASP.NET Core error dev page element
                if exception_elements:
                    # Get error message
                    error_message = await page.evaluate('(element) => element.textContent', exception_elements[0])
                    print(f"Error found on {url}: {error_message}")
                    error_found["value"] = True

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

def handle_console_message(msg, error_found):
    if msg.type == 'error':
        print(f"Console error: {msg.text}")
        error_found["value"] = True

if __name__ == "__main__":
    asyncio.get_event_loop().run_until_complete(run_tests())
