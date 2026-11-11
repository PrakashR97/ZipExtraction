
public void Main()
{
    bool fireAgain = false;

    try
    {
        Dts.Variables["User::IsCurrentMonth"].Value = false;

        string zipPath =
            Dts.Variables["User::ZipFilePath"].Value.ToString();

        string destinationRoot =
            Dts.Variables["User::DestinationRoot"].Value.ToString();

        System.IO.FileInfo file =
            new System.IO.FileInfo(zipPath);

        if (!file.Exists)
            throw new System.IO.FileNotFoundException(
                "ZIP file not found", zipPath);

        DateTime modifiedDate = file.LastWriteTime;
        DateTime today = DateTime.Now;

        bool isCurrentMonth =
            modifiedDate.Year == today.Year &&
            modifiedDate.Month == today.Month;

        Dts.Variables["User::ZipFileName"].Value =
            file.Name;

        Dts.Variables["User::ModifiedDate"].Value =
            modifiedDate;

        Dts.Variables["User::FileSize"].Value =
            file.Length;

        Dts.Variables["User::IsCurrentMonth"].Value =
            isCurrentMonth;

        Dts.Variables["User::ProcessingID"].Value =
            System.Guid.NewGuid().ToString();

        if (!isCurrentMonth)
        {
            Dts.Events.FireInformation(
                0, "ZIP Extraction",
                "Skipping other-month ZIP: " + file.Name,
                "", 0, ref fireAgain);

            Dts.TaskResult = (int)ScriptResults.Success;
            return;
        }

        string monthFolder =
            modifiedDate.ToString("yyyyMM");

        string monthPath =
            System.IO.Path.Combine(
                destinationRoot, monthFolder);

        System.IO.Directory.CreateDirectory(monthPath);

        string zipFolderName =
            System.IO.Path.GetFileNameWithoutExtension(
                file.Name);

        string destinationPath =
            System.IO.Path.Combine(
                monthPath, zipFolderName);

        Dts.Variables["User::MonthFolder"].Value =
            monthFolder;

        Dts.Variables["User::DestinationPath"].Value =
            destinationPath;

        Dts.Events.FireInformation(
            0, "ZIP Extraction",
            "File: " + file.Name +
            " | Modified: " + modifiedDate +
            " | Destination: " + destinationPath,
            "", 0, ref fireAgain);

        Dts.TaskResult = (int)ScriptResults.Success;
    }
    catch (Exception ex)
    {
        Dts.Events.FireError(
            0, "ZIP Extraction",
            ex.ToString(), "", 0);

        Dts.TaskResult = (int)ScriptResults.Failure;
    }
}




SELECT CASE
         WHEN EXISTS (
             SELECT 1
             FROM ZIP_EXTRACTION_LOG
             WHERE SOURCE_PATH = ?
               AND FILE_MODIFIED_DATE = CAST(? AS TIMESTAMP)
               AND FILE_SIZE = ?
               AND STATUS = 'SUCCESS'
         )
         THEN 0
         ELSE 1
       END AS SHOULD_PROCESS
FROM DUAL





INSERT INTO ZIP_EXTRACTION_LOG
(
    PROCESSING_ID,
    FILE_NAME,
    SOURCE_PATH,
    FILE_MODIFIED_DATE,
    FILE_SIZE,
    MONTH_FOLDER,
    DESTINATION_PATH,
    STATUS,
    START_TIME
)
VALUES
(
    ?,
    ?,
    ?,
    CAST(? AS TIMESTAMP),
    ?,
    ?,
    ?,
    'PROCESSING',
    SYSTIMESTAMP
)




public void Main()
{
    string tempPath = null;
    bool fireAgain = false;

    try
    {
        string zipPath =
            Dts.Variables["User::ZipFilePath"].Value.ToString();

        string destinationPath =
            Dts.Variables["User::DestinationPath"].Value.ToString();

        string processingID =
            Dts.Variables["User::ProcessingID"].Value.ToString();

        System.IO.FileInfo sourceFile =
            new System.IO.FileInfo(zipPath);

        if (!sourceFile.Exists)
            throw new System.IO.FileNotFoundException(
                "ZIP not found", zipPath);

        string expectedSource =
            "SOURCE_PATH=" + sourceFile.FullName;

        string expectedModified =
            "MODIFIED_UTC=" +
            sourceFile.LastWriteTimeUtc.Ticks;

        string expectedSize =
            "FILE_SIZE=" + sourceFile.Length;

        string markerName =
            "_extraction_complete.txt";

        // Recover an extraction already completed
        if (System.IO.Directory.Exists(destinationPath))
        {
            string markerPath =
                System.IO.Path.Combine(
                    destinationPath, markerName);

            if (!System.IO.File.Exists(markerPath))
                throw new System.IO.IOException(
                    "Destination exists without marker: " +
                    destinationPath);

            string[] lines =
                System.IO.File.ReadAllLines(markerPath);

            bool matching =
                System.Array.IndexOf(lines, expectedSource) >= 0 &&
                System.Array.IndexOf(lines, expectedModified) >= 0 &&
                System.Array.IndexOf(lines, expectedSize) >= 0;

            if (!matching)
                throw new System.IO.IOException(
                    "Existing folder belongs to a different ZIP version.");

            Dts.Events.FireInformation(
                0, "ZIP Extraction",
                "Existing completed extraction recovered: " +
                zipPath,
                "", 0, ref fireAgain);

            Dts.TaskResult = (int)ScriptResults.Success;
            return;
        }

        // Unique temporary folder
        tempPath = destinationPath + "_TEMP_" + processingID;

        System.IO.Directory.CreateDirectory(tempPath);

        // Extract ZIP contents
        System.IO.Compression.ZipFile.ExtractToDirectory(
            zipPath, tempPath);

        string markerPathNew =
            System.IO.Path.Combine(
                tempPath, markerName);

        // Avoid overwriting an archived file
        if (System.IO.File.Exists(markerPathNew))
            throw new System.IO.IOException(
                "ZIP contains reserved completion marker filename.");

        string markerContent =
            expectedSource + System.Environment.NewLine +
            expectedModified + System.Environment.NewLine +
            expectedSize + System.Environment.NewLine +
            "PROCESSING_ID=" + processingID;

        System.IO.File.WriteAllText(
            markerPathNew, markerContent);

        // Detect a destination created by another execution
        if (System.IO.Directory.Exists(destinationPath))
            throw new System.IO.IOException(
                "Destination created during extraction.");

        System.IO.Directory.Move(
            tempPath, destinationPath);

        Dts.Events.FireInformation(
            0, "ZIP Extraction",
            "Successfully extracted: " + zipPath,
            "", 0, ref fireAgain);

        Dts.TaskResult = (int)ScriptResults.Success;
    }
    catch (Exception ex)
    {
        try
        {
            if (tempPath != null &&
                System.IO.Directory.Exists(tempPath))
            {
                System.IO.Directory.Delete(tempPath, true);
            }
        }
        catch
        {
            // Preserve original exception
        }

        Dts.Events.FireError(
            0, "ZIP Extraction",
            ex.ToString(), "", 0);

        Dts.TaskResult = (int)ScriptResults.Failure;
    }
}








UPDATE ZIP_EXTRACTION_LOG
SET STATUS = 'SUCCESS',
    END_TIME = SYSTIMESTAMP,
    ERROR_MESSAGE = NULL
WHERE PROCESSING_ID = ?
  AND STATUS = 'PROCESSING'




UPDATE ZIP_EXTRACTION_LOG
SET STATUS = 'FAILED',
    END_TIME = SYSTIMESTAMP,
    ERROR_MESSAGE = 'ZIP extraction failed. Check SSIS execution logs.'
WHERE PROCESSING_ID = ?
  AND STATUS = 'PROCESSING'


