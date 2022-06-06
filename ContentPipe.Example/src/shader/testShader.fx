float4 MainPS(float4 col : COLOR0) : COLOR0
{
	return col;
}

technique Technique1
{
	pass Pass1
	{
		PixelShader = compile ps_3_0 MainPS();
	}
}