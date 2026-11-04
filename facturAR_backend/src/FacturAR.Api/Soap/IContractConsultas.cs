using System.ServiceModel;
using FacturAR.Contracts.Soap;

namespace FacturAR.Api.Soap;

[ServiceContract(Namespace = FecaeNamespaces.Fev1)]
public interface IContractConsultas
{
    [OperationContract(Action = FecaeNamespaces.Fev1 + "FECompUltimoAutorizado")]
    Task<FECompUltimoAutorizadoResponse> FECompUltimoAutorizado(FECompUltimoAutorizadoRequest request);

    [OperationContract(Action = FecaeNamespaces.Fev1 + "FECompConsultar")]
    Task<FECompConsultarResponse> FECompConsultar(FECompConsultarRequest request);

    [OperationContract(Action = FecaeNamespaces.Fev1 + "FEDummy")]
    Task<FEDummyResponse> FEDummy(FEDummyRequest? request = null);
}

