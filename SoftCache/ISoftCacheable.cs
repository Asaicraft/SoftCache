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
    /// Computes a lightweight 32-bit hash code representing the identity of this object.
    /// <para>
    /// Unlike the standard <see cref="object.GetHashCode"/>, which is typically optimized 
    /// for uniform distribution across large hash tables, <c>GetSoftHashCode</c> is optimized 
    /// for <b>speed</b> and for use in a fixed-size <c>SoftCache</c>.
    /// </para>
    /// <para>
    /// The <b>lowest 16 bits</b> of the returned value carry the most important 
    /// information and are used directly for bucket indexing. The upper 16 bits may 
    /// still hold useful entropy but are secondary.
    /// </para>
    /// <para>
    /// Implementations must ensure consistency with <see cref="SoftEquals"/>: 
    /// objects considered equal must produce identical hash codes.
    /// </para>
    /// </summary>
    /// <returns>
    /// A 32-bit unsigned integer hash code, where the lower 16 bits carry 
    /// the primary bucket-selection signal.
    /// </returns>
    public uint GetSoftHashCode();

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
