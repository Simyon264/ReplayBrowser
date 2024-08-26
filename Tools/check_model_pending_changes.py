# This script checks if there are any pending changes in the model and if so, it will fail.
# This is useful to prevent committing changes to the model that are not intended.
# It uses "dotnet ef migrations has-pending-model-changes"
# If it exits with "No changes have been made to the model since the last migration.", it means there are no pending changes. Hooraay!
# If anything else, it means there are pending changes and the script will fail. Booo!
# Note: The command will output some other output as well, but we are only interested in the message above.

import subprocess
import sys

def main():
    command = ['dotnet', 'ef', 'migrations', 'has-pending-model-changes']
    result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode != 0:
        print(f'Error: {result.stderr.decode("utf-8")}')
        sys.exit(1)

    output = result.stdout.decode("utf-8")
    if 'No changes have been made to the model since the last migration.' not in output:
        print(f'Error: There are pending changes in the model.')
        sys.exit(1)

    print('No pending changes in the model. Good to go!')

if __name__ == '__main__':
    main()