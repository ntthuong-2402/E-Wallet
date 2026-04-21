import os
from google import genai

# ===== CONFIG =====
API_KEY = "AIzaSyDC1SBYYBYD4ySuSpHjvLrqe42qx2ywMcM"
PROJECT_PATH = "./"   # folder project
MAX_CHARS = 120000    # tránh vượt context
# ==================

client = genai.Client(api_key=API_KEY)

code_files = []
supported_ext = (
    ".cs", ".csproj", ".sln",
    ".py",
    ".js", ".ts", ".jsx", ".tsx",
    ".json", ".yaml", ".yml"
)

# scan project
for root, dirs, files in os.walk(PROJECT_PATH):

    # bỏ folder không cần thiết
    dirs[:] = [d for d in dirs if d not in
               ["node_modules", "bin", "obj", ".git", "__pycache__"]]

    for file in files:
        if file.endswith(supported_ext):
            path = os.path.join(root, file)

            try:
                with open(path, "r", encoding="utf-8", errors="ignore") as f:
                    content = f.read()

                code_files.append(f"\nFILE: {path}\n{content}")

            except:
                pass

project_code = "\n".join(code_files)

# tránh vượt context
project_code = project_code[:MAX_CHARS]

prompt = f"""
You are a senior software architect.

Analyze this project and explain:

1. What architecture it uses
2. Main modules
3. Possible bugs
4. Code quality issues
5. Suggestions for improvement

Project code:
{project_code}
"""

response = client.models.generate_content(
    model="gemini-2.5-flash",
    contents=prompt
)

print("\n=== AI REVIEW ===\n")
print(response.text)