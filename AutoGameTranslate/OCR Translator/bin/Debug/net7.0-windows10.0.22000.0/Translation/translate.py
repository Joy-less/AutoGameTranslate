import os
from pathlib import Path
from time import sleep

CURRENT_DIRECTORY = str(Path(__file__).parent.resolve())
PARENT_DIRECTORY = str(Path(__file__).parent.parent.resolve())

# Get the language
print("Getting language...")
LANGUAGE_FILE = CURRENT_DIRECTORY + "/language.txt"
from_language = ""
to_language = ""
with open(LANGUAGE_FILE, "r") as f:
    contents = f.readlines()
    from_language = contents[0].strip()
    to_language = contents[1].strip()

print(from_language + " -> " + to_language)
# Get entries in current directory
print("Finding model...")
entries = os.listdir(PARENT_DIRECTORY)
# Get Argos models in current directory
models = []
for entry in entries:
    if entry.endswith(".argosmodel"):
        models.append(PARENT_DIRECTORY + "/" + entry)
# Find the model
translation_language_code = from_language + "-" + to_language
correct_model = None
for model in models:
    if model.find(translation_language_code) >= 0:
        correct_model = model
        break
if correct_model == None:
    input("Could not find model. Press enter to exit.")

# Install the model
print("Installing model...")
import argostranslate.package as package
import argostranslate.translate as translate

package.install_from_path(correct_model)

# Translate
print("Ready!")

INPUT_FILE = CURRENT_DIRECTORY + "/translate_input.txt"
OUTPUT_FILE = CURRENT_DIRECTORY + "/translate_output.txt"

while True:
    sleep(50 / 1000)
    if Path(INPUT_FILE).is_file():
        try:
            # Read the input file
            text_input = ""
            with open(INPUT_FILE, "r", encoding="utf-8") as f:
                text_input = f.read()
            # Delete the input file
            os.remove(INPUT_FILE)
            # Translate
            result = translate.translate(text_input, from_language, to_language)
            # Return result
            # print("Translated '" + text_input + "' to '" + result + "'")
            with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
                f.write(result)
        except:
            pass
