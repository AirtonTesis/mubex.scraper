#!/bin/bash
# Test script for real Playwright scraping

# 1. Login
echo "1. Login..."
TOKEN=$(curl -s http://localhost:5245/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"test@example.com","password":"TestPassword123!"}' \
  | python -c "import sys,json; print(json.load(sys.stdin)['token'])" 2>/dev/null)

if [ -z "$TOKEN" ]; then
  echo "ERROR: Login failed"
  exit 1
fi
echo "   Token OK"

# 2. Create search list
echo "2. Creating search list..."
BODY=$(python -c "import json; print(json.dumps({'name':'Empresas Embu','keywords':['estetica embu guacu'],'domains':[]}))")
curl -s -X POST http://localhost:5245/api/searchlists \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d "$BODY" && echo ""

# 3. Get list ID
echo "3. Getting list ID..."
LIST_ID=$(curl -s http://localhost:5245/api/searchlists \
  -H "Authorization: Bearer $TOKEN" \
  | python -c "import sys,json; d=json.load(sys.stdin); print(d[0]['id'] if d else 'none')" 2>/dev/null)
echo "   List ID: $LIST_ID"

if [ "$LIST_ID" = "none" ]; then
  echo "ERROR: No lists found"
  exit 1
fi

# 4. Enqueue job
echo "4. Enqueueing job..."
JOB_BODY=$(python -c "import json; print(json.dumps({'searchListId':'$LIST_ID'}))")
curl -s -X POST http://localhost:5245/api/jobs \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d "$JOB_BODY" && echo ""

echo ""
echo "Job enqueued! Waiting 60s for real Playwright scraping..."
echo "(Check /tmp/webapi_real_scrape.log for scraping activity)"

sleep 60

# 5. Check results
echo ""
echo "=== FINAL RESULTS ==="
echo "Jobs:"
curl -s http://localhost:5245/api/jobs \
  -H "Authorization: Bearer $TOKEN" \
  | python -c "
import sys,json
jobs=json.load(sys.stdin)
for j in jobs[:3]:
    print(f'  Status: {j[\"status\"]} | Items: {j.get(\"itemsCollected\",0)} | Error: {j.get(\"errorMessage\",\"none\")}')
" 2>/dev/null

echo ""
echo "Lists:"
curl -s http://localhost:5245/api/searchlists \
  -H "Authorization: Bearer $TOKEN" \
  | python -c "
import sys,json
lists=json.load(sys.stdin)
for l in lists[:3]:
    print(f'  {l[\"name\"]} | TotalItems: {l.get(\"totalItemsCollected\",0)}')
" 2>/dev/null
