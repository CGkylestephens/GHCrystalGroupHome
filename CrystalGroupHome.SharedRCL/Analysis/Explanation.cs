using System.Collections.Generic;

namespace CrystalGroupHome.SharedRCL.Analysis
{
    /// <summary>
    /// Represents an explanation for a difference found in MRP logs.
    /// </summary>
    public class Explanation
    {
        /// <summary>
        /// Brief summary of the explanation.
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// The difference this explanation relates to.
        /// </summary>
        public Difference RelatedDifference { get; set; } = new Difference();

        /// <summary>
        /// Facts - log-supported evidence.
        /// </summary>
        public List<Fact> Facts { get; set; } = new List<Fact>();

        /// <summary>
        /// Inferences - plausible explanations.
        /// </summary>
        public List<Inference> Inferences { get; set; } = new List<Inference>();

        /// <summary>
        /// Recommended next steps to check in Epicor.
        /// </summary>
        public List<string> NextStepsInEpicor { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents a fact - log-supported evidence.
    /// </summary>
    public class Fact
    {
        /// <summary>
        /// Statement describing the fact.
        /// </summary>
        public string Statement { get; set; } = string.Empty;

        /// <summary>
        /// Line number in the log where this fact was found.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Raw log evidence supporting this fact.
        /// </summary>
        public string? LogEvidence { get; set; }
    }

    /// <summary>
    /// Represents an inference - plausible explanation.
    /// </summary>
    public class Inference
    {
        /// <summary>
        /// Statement describing the inference.
        /// </summary>
        public string Statement { get; set; } = string.Empty;

        /// <summary>
        /// Confidence level (0.0 to 1.0).
        /// </summary>
        public double ConfidenceLevel { get; set; }

        /// <summary>
        /// Supporting reasons for this inference.
        /// </summary>
        public List<string> SupportingReasons { get; set; } = new List<string>();
    }
}
