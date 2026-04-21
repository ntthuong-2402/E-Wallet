from google import genai

client = genai.Client(api_key="AIzaSyDC1SBYYBYD4ySuSpHjvLrqe42qx2ywMcM")

models = client.models.list()

for model in models:
    print(model.name)