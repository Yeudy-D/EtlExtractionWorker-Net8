using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EtlExtractionWorker.Models;

namespace EtlExtractionWorker.Abstractions;

public interface IExtractor
{
    string SourceName { get; }
    Task<IReadOnlyList<ExtractedRecord>> ExtractAsync(CancellationToken cancellationToken = default);
}