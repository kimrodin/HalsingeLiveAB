namespace EventCatalog.Features.Venues.Models;

public enum SeatingType
{
    // TODO: Dokumentera att GeneralAdmission betyder fri placering och AssignedSeating numrerade stolar.
    // TODO: Bestäm hur enum-värden serialiseras i API:et och lagras i databasen; håll kontraktet stabilt.
    // TODO: Validera inkommande enum-värden så att okända numeriska värden avvisas.

    GeneralAdmission,
    AssignedSeating
}
