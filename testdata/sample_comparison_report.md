# MRP Log Comparison Report

*Generated: 2024-02-12 10:30:00*

## A) RUN SUMMARY

### Run A
- **Type**: Regeneration
- **Source**: `mrp_log_sample_A.txt`
- **Site**: PLANT01
- **Start Time**: 2024-02-01 02:00:00
- **End Time**: 2024-02-01 03:15:00
- **Duration**: 01:15:00
- **Entries Parsed**: 1,250
- **Errors Logged**: 3
- **Parts Processed**: 87
- **Jobs Referenced**: 142

### Run B
- **Type**: Net Change
- **Source**: `mrp_log_sample_B.txt`
- **Site**: PLANT01
- **Start Time**: 2024-02-02 02:00:00
- **End Time**: 2024-02-02 02:45:00
- **Duration**: 00:45:00
- **Entries Parsed**: 856
- **Errors Logged**: 1
- **Parts Processed**: 42
- **Jobs Referenced**: 98

## B) WHAT CHANGED

**Total Differences**: 3
- 🔴 Critical: 1
- ⚠️  Warning: 1
- ℹ️  Info: 1

### Top Changes:

🔴 **JobRemoved**: Job 14567 present in Run A but missing in Run B
  - Part: PART-12345
  - Job: 14567

⚠️  **JobAdded**: New job 14569 created in Run B
  - Part: PART-99999
  - Job: 14569

ℹ️  **DateShift**: Job 14568 processing time changed
  - Part: PART-67890
  - Job: 14568

### Summary by Type:
- Jobs Added: 1
- Jobs Removed: 1
- Date Shifts: 1
- Quantity Changes: 0
- New Errors: 0

## C) MOST LIKELY WHY

### Job 14567 disappeared between runs

**FACTS** (log-supported evidence):
- ✅ Job 14567 present in Run A at line 42
- ✅ Run A logged error: Job 14567 abandoned due to timeout

**INFERENCES** (plausible explanations):
- 🔍 Job may have been automatically removed due to timeout (Confidence: High)
  - Timeout errors often trigger automatic cleanup
  - Common in Net Change runs
  - No corresponding entry in Run B suggests automatic deletion

### New job 14569 created for part PART-99999

**FACTS** (log-supported evidence):
- ✅ Job 14569 first appears in Run B at line 52

**INFERENCES** (plausible explanations):
- 🔍 Likely triggered by new demand or inventory adjustment (Confidence: Medium)
  - New jobs typically result from demand changes
  - Common in Net Change processing

## D) LOG EVIDENCE

### Evidence for: Job 14567 disappeared between runs

**Line 42**:
```
02:30:00 Processing Part PART-12345 for Job 14567
```

**Line 58**:
```
02:45:00 ERROR: Job 14567 abandoned due to timeout
```

### Evidence for: New job 14569 created for part PART-99999

**Line 52**:
```
02:35:00 Processing Part PART-99999 for Job 14569 - New Job Created
```

## E) NEXT CHECKS IN EPICOR

### 🔴 Must Check:
- Check Job Tracker for deletion history of Job 14567
- Review System Monitor for timeout configuration

### ⚠️ Should Check:
- Verify if Part PART-12345 has alternate sourcing
- Review Part PART-99999 demand history
- Check sales orders for new requirements
