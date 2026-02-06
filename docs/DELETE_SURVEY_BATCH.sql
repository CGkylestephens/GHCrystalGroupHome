/*
==============================================================================
DELETE SURVEY BATCH - Administrative Script
==============================================================================
Purpose: Safely delete all database entries associated with a specific survey batch
         including responses, batch parts, and the batch itself.

WARNING: This operation is IRREVERSIBLE. Use with extreme caution.
         Always verify the batch ID before executing.

Usage:
1. Replace @BatchIdToDelete with the actual batch ID you want to delete
2. Review the SELECT statements to verify what will be deleted
3. Comment out the DELETE statements and run the SELECTs first to preview
4. Once verified, uncomment DELETE statements and execute
==============================================================================
*/

-- Set this to the batch ID you want to delete
DECLARE @BatchIdToDelete INT = 0;  -- *** CHANGE THIS VALUE ***

-- Verify batch exists and show details
PRINT '========================================';
PRINT 'BATCH DETAILS TO BE DELETED';
PRINT '========================================';

SELECT 
    b.Id AS BatchId,
    b.VendorNum,
    b.Status AS BatchStatus,
    b.SentDate,
    b.ResponseDueDate,
    b.CreatedDate,
    t.Name AS TemplateName,
    tv.VersionNumber AS TemplateVersion,
    COUNT(DISTINCT bp.Id) AS TotalParts
FROM [CGIExt].[dbo].[CMVendorComms_SurveyBatch] b
LEFT JOIN [CGIExt].[dbo].[CMVendorComms_SurveyTemplateVersion] tv ON b.SurveyTemplateVersionID = tv.Id
LEFT JOIN [CGIExt].[dbo].[CMVendorComms_SurveyTemplate] t ON tv.SurveyTemplateID = t.Id
LEFT JOIN [CGIExt].[dbo].[CMVendorComms_SurveyBatchPart] bp ON b.Id = bp.SurveyBatchID
WHERE b.Id = @BatchIdToDelete
GROUP BY b.Id, b.VendorNum, b.Status, b.SentDate, b.ResponseDueDate, b.CreatedDate, t.Name, tv.VersionNumber;

-- Show all parts in this batch
PRINT '';
PRINT '========================================';
PRINT 'PARTS IN THIS BATCH';
PRINT '========================================';

SELECT 
    bp.Id AS BatchPartId,
    bp.SubmissionStatus,
    pst.PartNum,
    pst.ext_PartDescription AS PartDescription,
    pst.ext_VendorName AS VendorName,
    (SELECT COUNT(*) FROM [CGIExt].[dbo].[CMVendorComms_SurveyResponse] r WHERE r.SurveyBatchPartID = bp.Id) AS ResponseCount
FROM [CGIExt].[dbo].[CMVendorComms_SurveyBatchPart] bp
INNER JOIN [CGIExt].[dbo].[CMVendorComms_PartStatusTracker] pst ON bp.PartStatusTrackerID = pst.Id
WHERE bp.SurveyBatchID = @BatchIdToDelete
ORDER BY pst.PartNum;

-- Show all responses that will be deleted
PRINT '';
PRINT '========================================';
PRINT 'RESPONSES TO BE DELETED';
PRINT '========================================';

SELECT 
    r.Id AS ResponseId,
    pst.PartNum,
    q.QuestionText,
    r.ResponseValue,
    r.ResponseReceivedDate
FROM [CGIExt].[dbo].[CMVendorComms_SurveyResponse] r
INNER JOIN [CGIExt].[dbo].[CMVendorComms_SurveyBatchPart] bp ON r.SurveyBatchPartID = bp.Id
INNER JOIN [CGIExt].[dbo].[CMVendorComms_PartStatusTracker] pst ON bp.PartStatusTrackerID = pst.Id
INNER JOIN [CGIExt].[dbo].[CMVendorComms_SurveyQuestion] q ON r.QuestionID = q.Id
WHERE bp.SurveyBatchID = @BatchIdToDelete
ORDER BY pst.PartNum, q.DisplayOrder;

-- Show summary counts
PRINT '';
PRINT '========================================';
PRINT 'DELETION SUMMARY';
PRINT '========================================';

SELECT 
    'Responses' AS RecordType,
    COUNT(*) AS RecordCount
FROM [CGIExt].[dbo].[CMVendorComms_SurveyResponse] r
INNER JOIN [CGIExt].[dbo].[CMVendorComms_SurveyBatchPart] bp ON r.SurveyBatchPartID = bp.Id
WHERE bp.SurveyBatchID = @BatchIdToDelete

UNION ALL

SELECT 
    'Batch Parts' AS RecordType,
    COUNT(*) AS RecordCount
FROM [CGIExt].[dbo].[CMVendorComms_SurveyBatchPart]
WHERE SurveyBatchID = @BatchIdToDelete

UNION ALL

SELECT 
    'Survey Batch' AS RecordType,
    COUNT(*) AS RecordCount
FROM [CGIExt].[dbo].[CMVendorComms_SurveyBatch]
WHERE Id = @BatchIdToDelete;

PRINT '';
PRINT '========================================';
PRINT 'SAFETY CHECK';
PRINT '========================================';

IF @BatchIdToDelete = 0
BEGIN
    PRINT 'ERROR: @BatchIdToDelete is set to 0. Please set a valid batch ID.';
    PRINT 'Deletion aborted for safety.';
END
ELSE
BEGIN
    PRINT 'Batch ID is set to: ' + CAST(@BatchIdToDelete AS VARCHAR(10));
    PRINT '';
    PRINT 'IMPORTANT: Review the information above carefully.';
    PRINT 'If everything looks correct, uncomment the DELETE section below and run again.';
    PRINT '';
    PRINT 'To proceed with deletion:';
    PRINT '1. Verify the batch details above are correct';
    PRINT '2. Uncomment the BEGIN TRANSACTION and DELETE statements below';
    PRINT '3. Run the script again';
    PRINT '4. Review the results';
    PRINT '5. Decide whether to COMMIT or ROLLBACK the transaction';
END

/*
==============================================================================
DELETION SECTION
==============================================================================
UNCOMMENT THE SECTION BELOW WHEN READY TO DELETE

Steps to use:
1. Make sure @BatchIdToDelete is set correctly above
2. Remove the comment markers /* and */ around this section
3. Execute the script
4. Review the output messages
5. If everything looks correct, run:   COMMIT TRANSACTION
6. If something is wrong, run:        ROLLBACK TRANSACTION
==============================================================================
*/

/*

-- Start transaction for safety
BEGIN TRANSACTION;

PRINT '';
PRINT '========================================';
PRINT 'STARTING DELETION PROCESS';
PRINT '========================================';

-- Variable to track affected rows
DECLARE @RowsAffected INT;

-- Step 1: Delete all survey responses for this batch
PRINT 'Step 1: Deleting survey responses...';

DELETE r
FROM [CGIExt].[dbo].[CMVendorComms_SurveyResponse] r
INNER JOIN [CGIExt].[dbo].[CMVendorComms_SurveyBatchPart] bp ON r.SurveyBatchPartID = bp.Id
WHERE bp.SurveyBatchID = @BatchIdToDelete;

SET @RowsAffected = @@ROWCOUNT;
PRINT 'Deleted ' + CAST(@RowsAffected AS VARCHAR(10)) + ' survey response(s)';

-- Step 2: Delete all batch parts for this batch
PRINT '';
PRINT 'Step 2: Deleting batch parts...';

DELETE FROM [CGIExt].[dbo].[CMVendorComms_SurveyBatchPart]
WHERE SurveyBatchID = @BatchIdToDelete;

SET @RowsAffected = @@ROWCOUNT;
PRINT 'Deleted ' + CAST(@RowsAffected AS VARCHAR(10)) + ' batch part(s)';

-- Step 3: Delete the survey batch itself
PRINT '';
PRINT 'Step 3: Deleting survey batch...';

DELETE FROM [CGIExt].[dbo].[CMVendorComms_SurveyBatch]
WHERE Id = @BatchIdToDelete;

SET @RowsAffected = @@ROWCOUNT;
PRINT 'Deleted ' + CAST(@RowsAffected AS VARCHAR(10)) + ' survey batch(es)';

PRINT '';
PRINT '========================================';
PRINT 'DELETION COMPLETE';
PRINT '========================================';
PRINT 'Transaction is pending. Review the results above.';
PRINT '';
PRINT 'To finalize deletion, run:    COMMIT TRANSACTION';
PRINT 'To undo all changes, run:     ROLLBACK TRANSACTION';
PRINT '';
PRINT 'WARNING: Once committed, this action cannot be undone!';

-- DO NOT COMMIT AUTOMATICALLY - Require manual decision
-- Uncomment ONE of these lines after reviewing:
-- COMMIT TRANSACTION;    -- Uncomment to finalize deletion
-- ROLLBACK TRANSACTION;  -- Uncomment to undo all changes

*/

/*
==============================================================================
NOTES AND WARNINGS
==============================================================================

1. FOREIGN KEY RELATIONSHIPS:
   - CMVendorComms_SurveyResponse references CMVendorComms_SurveyBatchPart
   - CMVendorComms_SurveyBatchPart references CMVendorComms_SurveyBatch
   - Must delete in order: Responses -> BatchParts -> Batch

2. WHAT IS NOT DELETED:
   - CMVendorComms_PartStatusTracker records (trackers remain)
   - CMVendorComms_TrackerLog entries (logs are preserved)
   - Survey template and questions (templates remain for future use)
   - Part data in Epicor (no ERP changes)

3. WHEN TO USE THIS:
   - Test surveys that should never have been sent
   - Duplicate batches created by accident
   - Surveys sent to wrong vendor
   - Data cleanup during development/testing

4. WHEN NOT TO USE THIS:
   - Production surveys with real vendor responses (archive instead)
   - Historical data needed for auditing
   - When in doubt - consult with stakeholders first

5. ALTERNATIVES TO DELETION:
   - Use the "Close Survey" feature in the app to mark as complete
   - Update batch Status to 'Closed' if just need to hide from active list
   - Export data before deletion for record keeping

6. SAFETY FEATURES:
   - Requires explicit batch ID (no wildcards)
   - Shows preview of what will be deleted
   - Uses transaction (can be rolled back)
   - Requires manual COMMIT (won't auto-commit)
   - Provides detailed logging output

7. AUDIT TRAIL:
   - Consider exporting data to Excel/CSV before deletion
   - Save this script output for records
   - Note the deletion in a change log or ticket system

==============================================================================
*/
