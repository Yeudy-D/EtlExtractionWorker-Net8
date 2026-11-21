using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EtlExtractionWorker.Models;

public record ExtractedRecord(
    string Source,
    string ExternalId,
    string Content,
    DateTime ExtractedAtUtc
);
