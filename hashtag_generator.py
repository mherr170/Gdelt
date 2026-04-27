"""
Gemini 2.5 Flash hashtag generator for GDELT news headlines.

Free tier limits: 10 RPM / 250 RPD
Install: pip install google-genai
Set env var: GEMINI_API_KEY=your_key_here
"""

import os
import time
import re
from google import genai
from google.genai import types

# ── Config ────────────────────────────────────────────────────────────────────

GEMINI_API_KEY = os.environ.get("GEMINI_API_KEY", "")
MODEL_ID = "gemini-2.5-flash"

SYSTEM_INSTRUCTION = """\
You are an expert hashtag generator for Bluesky posts about breaking news articles.

Given a news headline, output exactly 3 relevant hashtags — one per line, no # symbol, no explanation.

Rules:
- Acronyms stay uppercase: NATO, FBI, CIA, UN, WHO, EU, UK, US
- Multi-word concepts use PascalCase: MassShooting, NuclearDeal, ClimateChange
- Never use generic words: News, Breaking, Today, Story, Article, Update
- Be specific and contextually relevant — prefer named entities and concrete topics
- One hashtag per line, nothing else
"""

# ── Rate limit tracking ───────────────────────────────────────────────────────

_request_times: list[float] = []
REQUESTS_PER_MINUTE = 10
REQUESTS_PER_DAY = 250


def _check_rate_limit() -> None:
    """Remove timestamps older than 60s; block if at RPM limit."""
    now = time.monotonic()
    _request_times[:] = [t for t in _request_times if now - t < 60]
    if len(_request_times) >= REQUESTS_PER_MINUTE:
        wait = 60 - (now - _request_times[0]) + 0.5
        print(f"[rate limit] RPM cap reached — waiting {wait:.1f}s")
        time.sleep(wait)


# ── Client & chat session ─────────────────────────────────────────────────────

def make_client() -> genai.Client:
    if not GEMINI_API_KEY:
        raise ValueError(
            "GEMINI_API_KEY environment variable is not set.\n"
            "Get a free key at: https://aistudio.google.com/app/apikey"
        )
    return genai.Client(api_key=GEMINI_API_KEY)


def make_chat_session(client: genai.Client) -> genai.chats.Chat:
    """Create a stateful ChatSession with the system instruction pre-loaded."""
    return client.chats.create(
        model=MODEL_ID,
        config=types.GenerateContentConfig(
            system_instruction=SYSTEM_INSTRUCTION,
            temperature=0.2,   # low temp = consistent, on-topic output
            max_output_tokens=64,
        ),
    )


# ── Core generation ───────────────────────────────────────────────────────────

MAX_RETRIES = 3

def generate_hashtags_chat(chat: genai.chats.Chat, headline: str) -> list[str]:
    """
    Generate hashtags using a stateful ChatSession.
    The session remembers prior exchanges — useful if you want
    the model to learn your topic preferences over a session.
    """
    for attempt in range(1, MAX_RETRIES + 1):
        try:
            _check_rate_limit()
            response = chat.send_message(headline)
            _request_times.append(time.monotonic())
            return _parse_tags(response.text)

        except Exception as ex:
            if _is_rate_limit(ex):
                wait = _backoff(attempt)
                print(f"[429] Rate limited — retrying in {wait}s (attempt {attempt}/{MAX_RETRIES})")
                time.sleep(wait)
            else:
                raise

    raise RuntimeError("Gemini rate limit exceeded after all retries.")


def generate_hashtags_single(client: genai.Client, headline: str) -> list[str]:
    """
    Stateless single-turn call via client.models.generate_content.
    Use this when you don't need conversation history.
    """
    for attempt in range(1, MAX_RETRIES + 1):
        try:
            _check_rate_limit()
            response = client.models.generate_content(
                model=MODEL_ID,
                contents=headline,
                config=types.GenerateContentConfig(
                    system_instruction=SYSTEM_INSTRUCTION,
                    temperature=0.2,
                    max_output_tokens=64,
                ),
            )
            _request_times.append(time.monotonic())
            return _parse_tags(response.text)

        except Exception as ex:
            if _is_rate_limit(ex):
                wait = _backoff(attempt)
                print(f"[429] Rate limited — retrying in {wait}s (attempt {attempt}/{MAX_RETRIES})")
                time.sleep(wait)
            else:
                raise

    raise RuntimeError("Gemini rate limit exceeded after all retries.")


# ── Helpers ───────────────────────────────────────────────────────────────────

def _parse_tags(text: str) -> list[str]:
    """Strip # symbols, blank lines, and any extra prose from the response."""
    tags = []
    for line in text.splitlines():
        tag = re.sub(r"[^a-zA-Z0-9]", "", line.strip().lstrip("#"))
        if 2 <= len(tag) <= 50:
            tags.append(tag)
        if len(tags) == 3:
            break
    return tags


def _is_rate_limit(ex: Exception) -> bool:
    msg = str(ex).lower()
    return "429" in msg or "resource_exhausted" in msg or "rate" in msg


def _backoff(attempt: int) -> float:
    """Exponential backoff: 6s, 12s, 24s."""
    return 6 * (2 ** (attempt - 1))


# ── Demo / CLI ────────────────────────────────────────────────────────────────

if __name__ == "__main__":
    headlines = [
        "NATO allies pledge record military aid to Ukraine amid escalating conflict",
        "Federal Reserve signals interest rate pause as inflation cools",
        "Mass shooting at Texas shopping mall leaves 8 dead, dozens injured",
        "SpaceX Starship completes first successful orbital test flight",
        "WHO declares mpox outbreak a global health emergency",
    ]

    client = make_client()

    print("=== Single-turn (generate_content) ===")
    for h in headlines:
        tags = generate_hashtags_single(client, h)
        print(f"  {h[:60]}")
        print(f"  → #{' #'.join(tags)}\n")

    print("\n=== Chat session (stateful) ===")
    chat = make_chat_session(client)
    for h in headlines:
        tags = generate_hashtags_chat(chat, h)
        print(f"  {h[:60]}")
        print(f"  → #{' #'.join(tags)}\n")
