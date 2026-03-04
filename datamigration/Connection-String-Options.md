# Data Propagation Pipeline — Connection String Options

## The Problem

The data propagation pipeline needs **simultaneous access to two databases** 
(e.g., read from QA while writing to UAT). The current classic Release setup 
has one `ConnectionString` variable per stage, scoped so that the UAT stage 
can only see the UAT connection string — not QA's.

---

## Option A: New Variable Group with Named Connection Strings (Recommended)

**What to ask your DBA:**
"I need a new Variable Group in Azure DevOps Pipelines > Library called 
`DataPropagation-ConnectionStrings` with four secret variables:
- `SqlConnection_DEV`
- `SqlConnection_QA`
- `SqlConnection_UAT`
- `SqlConnection_PROD`

Each one is the full ADO.NET connection string for that database. This Variable 
Group will be used by a separate, manually-triggered YAML pipeline — it will not 
affect any existing Release definitions."

**Why this is best:**
- Completely independent from your existing Releases
- No risk of breaking the Sync API CI/CD pipeline
- DBA creates it once and you're done
- All four strings are available in every stage of the propagation pipeline
- The YAML pipeline I built is already configured for this approach

**DBA effort:** ~10 minutes (create Variable Group, paste 4 connection strings)

---

## Option B: Classic Release with Release-Scoped Variables

If your team strongly prefers the classic Release editor (the graphical UI), 
you could create a new classic Release definition and define the connection 
strings as **release-scoped** (not stage-scoped) variables.

**How it works:**
- Create a new Release definition (separate from your Sync API Release)
- Add variables at the **Release scope** (not stage scope):
  - `SqlConnection_DEV`
  - `SqlConnection_QA`
  - `SqlConnection_UAT`
  - `SqlConnection_PROD`
- Each stage runs a PowerShell task that calls the same script

**Downsides:**
- Classic Releases are being de-emphasized by Microsoft in favor of YAML
- Variable management is through the UI, not version-controlled
- You lose the "infrastructure as code" benefit of YAML

---

## Option C: Modify Existing Release Stages (Not Recommended)

You could try to add stage-scoped variables for the "other" database in each 
existing stage (e.g., add `SqlConnection_QA` to the UAT stage). 

**Why this is NOT recommended:**
- Clutters your Sync API Release with unrelated variables
- Risk of confusion between the Sync API's `ConnectionString` and the 
  propagation-specific variables
- Tightly couples two different concerns
- If someone accidentally edits the wrong variable, it could affect both 
  the Sync API deployment and data propagation

---

## Recommendation

**Go with Option A.** Ask your DBA to create the Variable Group. It's a 
10-minute task that keeps everything clean and independent. The YAML 
pipeline files live in your existing repo but operate as a completely 
separate pipeline. Your Sync API CI/CD is untouched.

Here's exactly what to send your DBA:

---

### Message to DBA

Subject: Request — New Variable Group for Data Propagation Pipeline

Hi [DBA],

I'm setting up a manually-triggered pipeline to copy lookup/rules table 
data between our Azure SQL environments (e.g., QA → UAT, QA → PROD). 
This is separate from our Sync API release pipeline.

I need a Variable Group created in Azure DevOps under Pipelines > Library:

**Name:** `DataPropagation-ConnectionStrings`

**Variables (all marked as secret):**

| Variable Name      | Points To                |
|-------------------|--------------------------|
| SqlConnection_DEV  | AHS Sync DEV database    |
| SqlConnection_QA   | AHS Sync QA database     |
| SqlConnection_UAT  | AHS Sync UAT database    |
| SqlConnection_PROD | AHS Sync PROD database   |

Each should be a full ADO.NET connection string with the same service 
account credentials used by our existing Release pipelines (or a 
dedicated account if you prefer).

The pipeline will need these permissions on each target database:
- SELECT (on source databases)
- INSERT, DELETE (on target databases)  
- ALTER on tables (to disable/enable FK constraints during data copy)

The PROD target has an approval gate so no data touches production 
without manual approval.

Let me know if you have any questions or if there's a preferred 
service account to use.

Thanks!
