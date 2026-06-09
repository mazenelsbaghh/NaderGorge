import json
import re
import requests
import uuid
from typing import Dict, List, Any

INVENTORY_PATH = "tests/endpoint_inventory.json"
BASE_URL = "http://localhost:5245"
REPORT_PATH = "specs/099-comprehensive-endpoint-testing/audit_report.md"

ROLES = {
    "Unauthenticated": None,
    "Student": {"phone": "20000000001", "surface": "student"},
    "Assistant": {"phone": "20000000003", "surface": "admin"},
    "Teacher": {"phone": "20000000004", "surface": "admin"},
    "Admin": {"phone": "20000000000", "surface": "admin"},
}

def login_all() -> Dict[str, str]:
    tokens = {}
    for role, credentials in ROLES.items():
        if credentials is None:
            continue
        try:
            url = f"{BASE_URL}/api/auth/login"
            payload = {
                "phoneNumber": credentials["phone"],
                "password": "password",
                "deviceFingerprint": f"test-harness-{role}-{uuid.uuid4()}",
                "deviceName": "Python Test Harness"
            }
            headers = {"X-App-Surface": credentials["surface"]}
            r = requests.post(url, json=payload, headers=headers, timeout=5)
            if r.status_code == 200:
                res_data = r.json()
                if res_data.get("success") and "data" in res_data:
                    tokens[role] = res_data["data"]["accessToken"]
                    print(f"Successfully authenticated as {role}")
                else:
                    print(f"Failed to log in as {role}: {res_data.get('message')}")
            else:
                print(f"Failed to log in as {role}: HTTP {r.status_code}")
        except Exception as e:
            print(f"Error logging in as {role}: {e}")
    return tokens

def substitute_params(path: str) -> str:
    # Pattern to match {paramName:guid} or {paramName}
    # E.g. {id:guid}, {commentId:guid}, {id}
    pattern = re.compile(r"\{[a-zA-Z0-9_]+(:[a-zA-Z0-9_]+)?\}")
    test_guid = "00000000-0000-0000-0000-000000000000"
    return pattern.sub(test_guid, path)

def run_tests():
    print("Loading endpoint inventory...")
    with open(INVENTORY_PATH, "r", encoding="utf-8") as f:
        inventory = json.load(f)
    
    endpoints = inventory.get("endpoints", [])
    print(f"Loaded {len(endpoints)} endpoints.")
    
    print("Authenticating roles...")
    tokens = login_all()
    
    results = []
    crashes = []
    
    for i, ep in enumerate(endpoints):
        controller = ep.get("controller")
        action = ep.get("action")
        method = ep.get("method", "GET").upper()
        raw_path = ep.get("path")
        
        # Skip E2E testing controller endpoints since they are E2E environment-only and throw 404/500 by design in Docker environment
        if controller == "E2eTestingController":
            continue
            
        substituted_path = substitute_params(raw_path)
        full_url = f"{BASE_URL}{substituted_path}"
        
        print(f"[{i+1}/{len(endpoints)}] Testing {method} {substituted_path} ...")
        
        endpoint_results = {
            "controller": controller,
            "action": action,
            "method": method,
            "path": raw_path,
            "roles": {}
        }
        
        for role in ROLES.keys():
            headers = {}
            if role in tokens:
                headers["Authorization"] = f"Bearer {tokens[role]}"
            
            # Pass app surface context for staff
            if role in ["Assistant", "Teacher", "Admin"]:
                headers["X-App-Surface"] = "admin"
            else:
                headers["X-App-Surface"] = "student"
                
            try:
                # Send empty JSON payload for body-accepting methods
                payload = {} if method in ["POST", "PUT", "PATCH"] else None
                
                r = requests.request(
                    method=method,
                    url=full_url,
                    json=payload,
                    headers=headers,
                    timeout=5
                )
                
                status_code = r.status_code
                response_text = r.text[:200]  # Get first 200 chars of response
                
                endpoint_results["roles"][role] = status_code
                
                if status_code == 500:
                    crashes.append({
                        "controller": controller,
                        "action": action,
                        "method": method,
                        "path": raw_path,
                        "role": role,
                        "response": response_text
                    })
                    print(f"  -> CRASH (500) for role {role}!")
                    
            except Exception as e:
                endpoint_results["roles"][role] = f"ERROR: {str(e)[:50]}"
                print(f"  -> Request error for role {role}: {e}")
                
        results.append(endpoint_results)
        
    write_markdown_report(results, crashes)

def write_markdown_report(results: List[Dict[str, Any]], crashes: List[Dict[str, Any]]):
    print(f"Generating Markdown audit report at {REPORT_PATH}...")
    
    total_endpoints = len(results)
    total_requests = total_endpoints * len(ROLES)
    total_500s = len(crashes)
    
    md = []
    md.append("# Endpoint Security & Reliability Audit Report")
    md.append(f"\n**Total Endpoints Tested**: {total_endpoints}")
    md.append(f"**Total Requests Executed**: {total_requests}")
    md.append(f"**Total Unhandled Crashes (500)**: {total_500s}")
    md.append(f"**Reliability Score**: {((total_requests - total_500s) / total_requests * 100):.2f}%")
    
    if total_500s > 0:
        md.append("\n## 🚨 Unhandled Crashes (500 Internal Server Error)")
        md.append("\nThe following endpoints crashed with an unhandled exception. These need to be resolved to ensure reliability:")
        md.append("\n| Method | Path | Role | Controller | Action | Response Snippet |")
        md.append("|---|---|---|---|---|---|")
        for crash in crashes:
            escaped_response = crash['response'].replace("\n", " ").replace("|", "\\|")
            md.append(f"| {crash['method']} | `{crash['path']}` | {crash['role']} | {crash['controller']} | {crash['action']} | `{escaped_response}` |")
    else:
        md.append("\n## ✅ No Unhandled Crashes (500) Found!")
        md.append("\nAll endpoints gracefully handled bad input and default Guid parameters without crashing Kestrel.")
        
    md.append("\n## 🔒 Role-Based Access Control matrix")
    md.append("\nBelow is the status code matrix for every endpoint tested under each security context:")
    md.append("\n| Controller | Method | Path | Unauth | Student | Assistant | Teacher | Admin |")
    md.append("|---|---|---|---|---|---|---|---|")
    
    for r in results:
        roles_statuses = []
        for role in ROLES.keys():
            status = r["roles"].get(role, "N/A")
            # Style 500 red, 2xx green, others normal
            if status == 500:
                roles_statuses.append(f"**`500`**")
            elif isinstance(status, int) and 200 <= status < 300:
                roles_statuses.append(f"`{status}`")
            else:
                roles_statuses.append(f"`{status}`")
                
        md.append(f"| {r['controller']} | {r['method']} | `{r['path']}` | " + " | ".join(roles_statuses) + " |")
        
    with open(REPORT_PATH, "w", encoding="utf-8") as f:
        f.write("\n".join(md))
        
    print("Markdown audit report generated successfully.")

if __name__ == "__main__":
    run_tests()
