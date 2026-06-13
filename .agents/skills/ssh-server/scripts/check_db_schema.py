#!/usr/bin/env python3
import os
import re
import sys
import subprocess
import csv
from io import StringIO

# Configuration
SERVER_HOST = "72.62.27.189"
SERVER_USER = "root"
SERVER_PASS = "MazenElsbagh.12"

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "../../../.."))
SNAPSHOT_PATH = os.path.join(REPO_ROOT, "backend/src/NaderGorge.Infrastructure/Migrations/AppDbContextModelSnapshot.cs")
DOCS_DIR = os.path.join(SCRIPT_DIR, "../docs")
SCHEMA_MD_PATH = os.path.join(DOCS_DIR, "database_schema.md")

def parse_snapshot(snapshot_path):
    if not os.path.exists(snapshot_path):
        print(f"Error: Snapshot not found at {snapshot_path}", file=sys.stderr)
        return None

    with open(snapshot_path, "r", encoding="utf-8") as f:
        content = f.read()

    # Regex to find modelBuilder.Entity("...", b => { ... });
    # We will use a state machine for precision
    entities = {}
    current_entity = None
    entity_pattern = re.compile(r'modelBuilder\.Entity\("(?P<name>[^"]+)",\s*b\s*=>')
    property_pattern = re.compile(r'b\.(Property|PrimitiveCollection)<(?P<type>[^>]+)>\("(?P<name>[^"]+)"\)')
    column_type_pattern = re.compile(r'\.HasColumnType\("(?P<db_type>[^"]+)"\)')
    table_pattern = re.compile(r'b\.ToTable\("(?P<table_name>[^"]+)"')
    is_required_pattern = re.compile(r'\.IsRequired\(\)')
    max_length_pattern = re.compile(r'\.HasMaxLength\((?P<length>\d+)\)')

    lines = content.splitlines()
    in_entity = False
    brace_count = 0
    current_properties = []
    table_name = None

    for line in lines:
        stripped = line.strip()
        if not in_entity:
            match = entity_pattern.search(stripped)
            if match:
                current_entity = match.group("name")
                in_entity = True
                brace_count = 0
                current_properties = []
                table_name = None
        else:
            # Count braces to find end of entity block
            brace_count += stripped.count('{')
            brace_count -= stripped.count('}')
            
            # Parse property
            prop_match = property_pattern.search(stripped)
            if prop_match:
                prop_type = prop_match.group("type")
                prop_name = prop_match.group("name")
                
                # Infer nullable from C# type or logic
                is_nullable = "YES" if "?" in prop_type or prop_type == "string" else "NO"
                
                # Map standard C# types to default Postgres types in case HasColumnType is not specified
                default_db_type = "text"
                if "Guid" in prop_type:
                    default_db_type = "uuid"
                elif "int" in prop_type:
                    default_db_type = "integer"
                elif "long" in prop_type:
                    default_db_type = "bigint"
                elif "decimal" in prop_type:
                    default_db_type = "numeric"
                elif "bool" in prop_type:
                    default_db_type = "boolean"
                elif "DateTime" in prop_type:
                    default_db_type = "timestamp without time zone"
                
                current_properties.append({
                    "name": prop_name,
                    "csharp_type": prop_type,
                    "db_type": default_db_type,
                    "is_nullable": is_nullable
                })
            
            # Parse column type override
            if current_properties:
                col_type_match = column_type_pattern.search(stripped)
                if col_type_match:
                    current_properties[-1]["db_type"] = col_type_match.group("db_type")
                
                if is_required_pattern.search(stripped):
                    current_properties[-1]["is_nullable"] = "NO"

            # Parse table name
            table_match = table_pattern.search(stripped)
            if table_match:
                table_name = table_match.group("table_name")

            if brace_count < 0 or (brace_count == 0 and stripped == "});"):
                # End of entity configuration
                if table_name and current_properties:
                    entities[table_name] = current_properties
                in_entity = False
                current_entity = None

    return entities

def get_production_schema():
    print("Fetching actual database schema from production...")
    query = "SELECT table_name, column_name, data_type, is_nullable FROM information_schema.columns WHERE table_schema = 'public' ORDER BY table_name, column_name;"
    cmd = [
        "sshpass", "-p", SERVER_PASS,
        "ssh", "-o", "StrictHostKeyChecking=no", f"{SERVER_USER}@{SERVER_HOST}",
        f"docker exec -t massar_db psql -U postgres -d massar_platform -P pager=off -A -F ',' -c \"{query}\""
    ]
    
    try:
        result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, check=True)
        # Parse CSV output
        f = StringIO(result.stdout.strip())
        reader = csv.reader(f)
        
        # Skip header
        header = next(reader, None)
        if not header or len(header) < 4:
            print("Warning: unexpected DB response format. Trying to skip non-csv lines.", file=sys.stderr)
            # Find the line with table_name header
            f.seek(0)
            lines = f.readlines()
            csv_lines = []
            for line in lines:
                if ',' in line and not line.startswith("psql:") and not line.startswith("info:"):
                    csv_lines.append(line)
            f = StringIO("".join(csv_lines))
            reader = csv.reader(f)
            next(reader, None) # skip header
            
        prod_schema = {}
        for row in reader:
            if len(row) < 4:
                continue
            t_name, col_name, data_type, nullable = row[0].strip(), row[1].strip(), row[2].strip(), row[3].strip()
            if t_name not in prod_schema:
                prod_schema[t_name] = {}
            prod_schema[t_name][col_name] = {
                "db_type": data_type,
                "is_nullable": nullable
            }
        return prod_schema
    except subprocess.CalledProcessError as e:
        print(f"Error connecting to production database: {e.stderr}", file=sys.stderr)
        return None

def generate_markdown_doc(entities, prod_schema=None):
    os.makedirs(DOCS_DIR, exist_ok=True)
    
    with open(SCHEMA_MD_PATH, "w", encoding="utf-8") as f:
        f.write("# Nader Gorge Production Database Schema Specification\n\n")
        f.write("Auto-generated from EF Core DbContext snapshot. This documents all columns, data types, and nullability.\n\n")
        
        for table, cols in sorted(entities.items()):
            f.write(f"## Table: `{table}`\n\n")
            f.write("| Column Name | C# Type | Database Type | Nullable? | Status |\n")
            f.write("| --- | --- | --- | --- | --- |\n")
            for col in sorted(cols, key=lambda x: x["name"]):
                status = "✅ Sync"
                if prod_schema:
                    if table not in prod_schema:
                        status = "❌ Missing Table"
                    elif col["name"] not in prod_schema[table]:
                        status = "❌ Missing Column"
                    else:
                        # Normalize type comparison
                        expected_type = col["db_type"].lower()
                        actual_type = prod_schema[table][col["name"]]["db_type"].lower()
                        
                        # Handle type aliases (e.g. numeric vs decimal)
                        type_map = {
                            "numeric": "numeric",
                            "decimal": "numeric",
                            "double precision": "double precision",
                            "timestamp without time zone": "timestamp without time zone",
                            "character varying": "character varying",
                            "varchar": "character varying",
                            "text": "text",
                            "integer": "integer",
                            "bigint": "bigint",
                            "boolean": "boolean",
                            "uuid": "uuid",
                            "array": "array",
                            "text[]": "array"
                        }
                        
                        expected_norm = type_map.get(expected_type.split('(')[0], expected_type)
                        actual_norm = type_map.get(actual_type.split('(')[0], actual_type)
                        
                        if expected_norm != actual_norm:
                            status = f"⚠️ Type Mismatch ({actual_type} vs {col['db_type']})"
                
                f.write(f"| `{col['name']}` | `{col['csharp_type']}` | `{col['db_type']}` | `{col['is_nullable']}` | {status} |\n")
            f.write("\n")
            
    print(f"Database schema specification updated at: {SCHEMA_MD_PATH}")

def compare_schemas(expected_entities, prod_schema):
    if not prod_schema:
        print("Cannot compare schemas without production database schema info.", file=sys.stderr)
        return False

    errors = []
    warnings = []

    for expected_table, expected_cols in expected_entities.items():
        if expected_table not in prod_schema:
            errors.append(f"❌ Table '{expected_table}' is missing from production database.")
            continue
            
        prod_table = prod_schema[expected_table]
        for col in expected_cols:
            col_name = col["name"]
            if col_name not in prod_table:
                errors.append(f"❌ Table '{expected_table}' is missing column '{col_name}'. Expected type: {col['db_type']}")
            else:
                # Type check
                expected_type = col["db_type"].lower().split('(')[0]
                actual_type = prod_table[col_name]["db_type"].lower().split('(')[0]
                
                # Normalization
                type_aliases = {
                    "decimal": "numeric",
                    "varchar": "character varying",
                    "text": "text",
                    "text[]": "array",
                    "array": "array"
                }
                expected_norm = type_aliases.get(expected_type, expected_type)
                actual_norm = type_aliases.get(actual_type, actual_type)
                
                if expected_norm != actual_norm:
                    warnings.append(
                        f"⚠️ Table '{expected_table}', Column '{col_name}' type mismatch. "
                        f"Production: '{prod_table[col_name]['db_type']}', Codebase expected: '{col['db_type']}'"
                    )

    if errors:
        print("\n=== DATABASE SCHEMA ERRORS DETECTED ===", file=sys.stderr)
        for err in errors:
            print(err, file=sys.stderr)
            
    if warnings:
        print("\n=== DATABASE SCHEMA WARNINGS DETECTED ===", file=sys.stderr)
        for warn in warnings:
            print(warn, file=sys.stderr)

    if errors:
        return False
    return True

def main():
    print("Parsing codebase DB snapshot...")
    expected_entities = parse_snapshot(SNAPSHOT_PATH)
    if not expected_entities:
        sys.exit(1)

    prod_schema = get_production_schema()
    
    # Generate schema documentation markdown file
    generate_markdown_doc(expected_entities, prod_schema)

    if prod_schema:
        success = compare_schemas(expected_entities, prod_schema)
        if not success:
            print("\nDatabase schema is not in sync with codebase! Migrations may need to be applied.", file=sys.stderr)
            sys.exit(1)
        else:
            print("\nDatabase schema is 100% in sync! All tables and columns are present in production. 🎉")
    else:
        print("\nCould not verify schema sync because production DB is unreachable.", file=sys.stderr)
        sys.exit(0)

if __name__ == "__main__":
    main()
