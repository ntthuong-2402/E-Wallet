import sys
import os

import chromadb
from sentence_transformers import SentenceTransformer
from google import genai
from dotenv import load_dotenv
from google.genai.errors import ClientError

# =========================
# ENV
# =========================
load_dotenv()

sys.stdin.reconfigure(encoding="utf-8")
sys.stdout.reconfigure(encoding="utf-8")

API_KEY = os.getenv("GEMINI_API_KEY")

if not API_KEY:
    print("Missing GEMINI_API_KEY")
    exit(1)

client_ai = genai.Client(api_key=API_KEY)

# =========================
# MODEL + VECTOR DB
# =========================
model = SentenceTransformer("paraphrase-multilingual-MiniLM-L12-v2")

chroma_client = chromadb.PersistentClient(path="./chroma_db")
collection = chroma_client.get_or_create_collection("project_code")

# =========================
# MEMORY
# =========================
history = []

# =========================
# ROUTER
# =========================
def router(q: str):
    q = q.lower()

    # tất cả đều đi AI (vì không còn file ops)
    return "ai"

# =========================
# GEMINI CALL (NO RETRY, NO SLEEP)
# =========================
def call_ai(prompt: str):
    try:
        res = client_ai.models.generate_content(
            model="gemini-2.5-flash",
            contents=prompt
        )
        return res.text
    except ClientError as e:
        return f"SYSTEM ERROR: Gemini failed ({e})"

# =========================
# MAIN LOOP
# =========================
print("IDE Agent (analysis-only mode) ready\n")

while True:

    question = input("You: ")

    if question.lower() == "exit":
        break

    route = router(question)

    # =========================
    # RAG SEARCH (CODE CONTEXT)
    # =========================
    embedding = model.encode(question).tolist()

    results = collection.query(
        query_embeddings=[embedding],
        n_results=3
    )

    docs = results["documents"][0]
    metas = results["metadatas"][0]

    context = "\n\n".join(
        f"{m.get('path','unknown')}\n{d}"
        for d, m in zip(docs, metas)
    )

    history_text = "\n".join(history[-4:])

    # =========================
    # PROMPT
    # =========================
    prompt = f"""
You are a Senior Software Engineer specialized in C# (.NET), with deep expertise in:

- Microservices architecture
- Monolithic systems
- Distributed systems design
- Clean Architecture / DDD
- Performance optimization in .NET
- ASP.NET Core, gRPC, REST APIs
- Messaging systems (Kafka, RabbitMQ)
- Database design (SQL Server, PostgreSQL)
- System scalability and reliability

You think like a senior engineer working in large-scale enterprise systems.

=========================
RULES
=========================
- Be precise, technical, and practical
- Prefer real-world production patterns
- Avoid vague explanations
- If relevant, give C# examples
- Always consider scalability, maintainability, and trade-offs
- If comparing monolith vs microservices, explain clearly trade-offs
- Assume system is production-grade

=========================
CONTEXT (CODEBASE)
=========================
{context}

=========================
CONVERSATION HISTORY
=========================
{history_text}

=========================
USER QUESTION
=========================
{question}

=========================
OUTPUT STYLE
=========================
- Structured explanation
- Bullet points when needed
- Include C# snippets if helpful
- Mention architecture decisions explicitly
"""

    # =========================
    # CALL AI
    # =========================
    answer = call_ai(prompt)

    print("\nAI:\n", answer, "\n")

    # =========================
    # MEMORY
    # =========================
    history.append(f"Q: {question}")
    history.append(f"A: {answer}")