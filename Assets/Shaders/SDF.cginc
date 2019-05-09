// http://iquilezles.org/www/articles/distfunctions/distfunctions.htm

float sdSphere(float3 p, float s)
{
    return length(p) - s;
}

float sdBox(float3 p, float3 b)
{
    float3 d = abs(p) - b;
    return length(max(d, 0.0)) + min(max(d.x, max(d.y, d.z)), 0.0);
}

float sdPlane(float3 p, float4 n)
{
    // n must be normalized
    return dot(p, n.xyz) + n.w;
}


///
/// Primitive combinations
///

float opUnion(float d1, float d2)
{
    return min(d1, d2);
}

float4 opUnion(float4 d1, float4 d2)
{
    return d1.w < d2.w ? d1 : d2;
}

float opSubtraction(float d1, float d2)
{
    return max(-d1, d2);
}

float4 opSubstraction(float4 d1, float4 d2)
{
    return -d1.w > d2.w ? float4(d1.xyz, -d1.w) : d2;
}

float opIntersection(float d1, float d2)
{
    return max(d1, d2);
}

float4 opIntersection(float4 d1, float4 d2)
{
    return d1.w > d2.w ? d1 : d2;
}