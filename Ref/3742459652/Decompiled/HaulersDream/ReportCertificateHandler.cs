using UnityEngine.Networking;

namespace HaulersDream;

internal sealed class ReportCertificateHandler : CertificateHandler
{
	protected override bool ValidateCertificate(byte[] certificateData)
	{
		return true;
	}
}
