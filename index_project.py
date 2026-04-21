import os
import chromadb
from sentence_transformers import SentenceTransformer

# model embedding
##model = SentenceTransformer("all-MiniLM-L6-v2")
model = SentenceTransformer("paraphrase-multilingual-MiniLM-L12-v2")
# tạo database
client = chromadb.PersistentClient(path="./chroma_db")

collection = client.get_or_create_collection(
    name="project_code"
)

PROJECT_PATH = "./"

supported_ext = (
    ".cs",".py",".js",".ts",".json", ".props"
)

documents = []
metadatas = []
ids = []

i = 0

for root, dirs, files in os.walk(PROJECT_PATH):

    dirs[:] = [d for d in dirs if d not in
               ["node_modules","bin","obj",".git"]]

    for file in files:

        if file.endswith(supported_ext):

            path = os.path.join(root,file)

            try:
                with open(path,"r",encoding="utf-8",errors="ignore") as f:
                    code = f.read()

                documents.append(code)
                metadatas.append({"path": path})
                ids.append(str(i))

                i += 1

            except:
                pass

embeddings = model.encode(documents).tolist()

collection.add(
    documents=documents,
    embeddings=embeddings,
    metadatas=metadatas,
    ids=ids
)

print("Project indexed.")