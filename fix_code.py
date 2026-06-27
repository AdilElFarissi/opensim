import os
import json
from openai import OpenAI

# Configure connection to OpenRouter API endpoint
client = OpenAI(
    base_url="https://openrouter.ai",
    api_key=os.environ.get("OPENROUTER_API_KEY")
)

# 1. Load your custom OpenSim ruleset
rules = ""
if os.path.exists(".github/copilot-instructions.md"):
    with open(".github/copilot-instructions.md", "r") as f:
        rules = f.read()

# 2. Parse Semgrep security report logs
if not os.path.exists("semgrep-results.sarif"):
    print("No Semgrep results found to fix.")
    exit(0)

with open("semgrep-results.sarif", "r") as f:
    sarif_data = json.load(f)

# Extract target files containing flagged vulnerabilities
runs = sarif_data.get("runs", [])
for run in runs:
    for result in run.get("results", []):
        msg = result.get("message", {}).get("text", "")
        for loc in result.get("locations", []):
            uri = loc.get("physicalLocation", {}).get("artifactLocation", {}).get("uri", "")
            
            if uri and os.path.exists(uri):
                print(f"Repairing file: {uri}")
                with open(uri, "r") as f_code:
                    original_code = f_code.read()

                # Target a strong free coding model from OpenRouter
                response = client.chat.completions.create(
                    model="qwen/qwen3-coder-next:free",
                    messages=[
                        {"role": "system", "content": f"You are an expert C# compiler assistant. Fix the following code vulnerability. Adhere strictly to these architecture guidelines:\n{rules}\nReturn ONLY the raw fixed code. Do not include markdown code block syntax (like ```csharp)."},
                        {"role": "user", "content": f"Vulnerability Details: {msg}\n\nFile Content:\n{original_code}"}
                    ]
                )
                
                fixed_code = response.choices[0].message.content.strip()
                # Clean up accidental LLM markdown wrapping if present
                if fixed_code.startswith("```"):
                    fixed_code = "\n".join(fixed_code.split("\n")[1:-1])

                with open(uri, "w") as f_code_w:
                    f_code_w.write(fixed_code)

print("Code repair cycle successfully completed.")
