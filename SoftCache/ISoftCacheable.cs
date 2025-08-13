namespace SoftCache;

/// <summary>
/// Represents a type that can participate in a <c>SoftCache</c> lookup and storage operation.
/// <para>
/// <b>Important:</b> This interface is intended to be implemented <b>only</b> 
/// by code generators. Do not implement it manually in user code.
/// </para>
/// </summary>
/// <typeparam name="TParameters">
/// The parameter structure type used to describe the identity of this object 
/// for caching purposes. 
/// <para>
/// Must be an immutable <c>struct</c> (or <c>ref struct</c>) that contains 
/// all fields required for <see cref="SoftEquals"/> comparison and hash computation.
/// </para>
/// </typeparam>
/// <remarks>
/// <para>
/// This interface defines the minimal contract between a generated type and 
/// the <c>SoftCache</c> runtime. 
/// </para>
/// <para>
/// Implementations are expected to be immutable and safe to share between threads.
/// </para>
/// <para>
/// The generator will:
/// <list type="bullet">
///   <item>
///     Generate a strongly-typed <c>TParameters</c> containing the constructor 
///     parameters (or other key data) of the object.
///   </item>
///   <item>
///     Implement <see cref="SoftEquals"/> and <see cref="GetSoftHashCode"/> 
///     using the chosen <c>SoftHashKind</c>.
///   </item>
/// </list>
/// </para>
/// </remarks>
public interface ISoftCacheable<TParameters> where TParameters : struct, allows ref struct
{

    /// <summary>
    /// Determines whether this object is considered equivalent to the given parameter set
    /// for the purposes of cache lookup.
    /// <para>
    /// This method must perform a fast, field-by-field comparison of the parameters 
    /// that define the object's identity. 
    /// </para>
    /// <para>
    /// The generator ensures that <c>TParameters</c> contains all necessary 
    /// immutable data to make this decision without constructing a new instance.
    /// </para>
    /// </summary>
    /// <param name="parameters">
    /// The immutable parameters describing a candidate object to compare against this instance.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the parameter values match this instance's identity; 
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool SoftEquals(scoped in TParameters parameters);

    /// <summary>
    /// Computes a compact 16-bit hash code representing the identity of this object.
    /// <para>
    /// This value is used by <c>SoftCache</c> to index into its fixed-size cache array.
    /// </para>
    /// <para>
    /// <b>Note:</b> The generator implements this method based on the chosen hash strategy.
    /// It should be consistent with <see cref="SoftEquals"/> — equal objects must return the same hash.
    /// </para>
    /// </summary>
    /// <returns>
    /// A 16-bit unsigned integer hash code for this object.
    /// </returns>
    public ushort GetSoftHashCode();

    /// <summary>
    /// Retrieves the immutable parameter structure (<typeparamref name="TParameters"/>) 
    /// that represents the identity of this object for caching purposes.
    /// <para>
    /// The returned value contains all fields used in <see cref="SoftEquals"/> and 
    /// <see cref="GetSoftHashCode"/>, in the exact order expected by the generated code.
    /// </para>
    /// <para>
    /// This method is implemented by the source generator and should never be 
    /// implemented manually.
    /// </para>
    /// </summary>
    /// <returns>
    /// A <typeparamref name="TParameters"/> value encapsulating the identity-defining
    /// data of this instance.
    /// </returns>
    public TParameters GetParameters();
}
