# This script checks if there are any pending changes in the model and if so, it will fail.
# This is useful to prevent committing changes to the model that are not intended.

import subprocess
import sys

def main():
    try:
        # Run the dotnet ef migrations has-pending-model-changes command
        result = subprocess.run(
            ['dotnet', 'ef', 'migrations', 'has-pending-model-changes', '--project', './ReplayBrowser/ReplayBrowser.csproj',
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True
        )

        # Check the output and return code
        if result.returncode == 0:
            print("No pending model changes detected.")
        else:
            print("Pending model changes detected:")
            print(result.stdout)
            sys.exit(1)

    except Exception as e:
        print(f"An error occurred while checking for pending migrations: {str(e)}")
        sys.exit(1)

if __name__ == '__main__':
    main()