import { useEffect, useMemo, useRef, useState } from 'react'
import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import {
  Alert,
  Button,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { eventPageSize, getEventPage } from '../api/events'
import { getVenues } from '../../venues/api/venues'
import { useNow } from '../../../shared/hooks/useNow'
import EventCarousel from '../components/EventCarousel'
import EventCard from '../components/EventCard'
import bannerImage from '../../../assets/banner.png'

export default function HomePage() {
  const now = useNow()
  const [searchTerm, setSearchTerm] = useState('')
  const [debouncedSearchTerm, setDebouncedSearchTerm] = useState('')
  const loadMoreRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const timeout = window.setTimeout(() => setDebouncedSearchTerm(searchTerm.trim()), 300)
    return () => window.clearTimeout(timeout)
  }, [searchTerm])

  const eventsQuery = useInfiniteQuery({
    queryKey: ['events', 'catalog', debouncedSearchTerm, Math.floor(now / 60_000)],
    queryFn: ({ pageParam, signal }) => getEventPage(
      pageParam,
      { eventName: debouncedSearchTerm || undefined, endsAfter: new Date(now).toISOString() },
      signal,
    ),
    initialPageParam: 1,
    getNextPageParam: (lastPage) =>
      lastPage.page * lastPage.pageSize < lastPage.totalCount ? lastPage.page + 1 : undefined,
  })
  const venuesQuery = useQuery({
    queryKey: ['venues'],
    queryFn: ({ signal }) => getVenues(signal),
  })
  const {
    hasNextPage,
    isFetchingNextPage,
    isFetchNextPageError,
    fetchNextPage,
  } = eventsQuery

  useEffect(() => {
    const target = loadMoreRef.current
    if (!target || !hasNextPage || isFetchingNextPage || isFetchNextPageError) return

    const observer = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) void fetchNextPage()
    }, { rootMargin: '300px' })
    observer.observe(target)
    return () => observer.disconnect()
  }, [hasNextPage, isFetchingNextPage, isFetchNextPageError, fetchNextPage])

  const venuesById = useMemo(
    () => new Map(venuesQuery.data?.map((venue) => [venue.id, `${venue.name}, ${venue.city}`]) ?? []),
    [venuesQuery.data],
  )

  const events = eventsQuery.data?.pages.flatMap((page) => page.events) ?? []
  const visibleEvents = events.filter((event) => new Date(event.endDate).getTime() >= now)
  const featuredEvents = visibleEvents.slice(0, 3)
  const totalCount = eventsQuery.data?.pages[0]?.totalCount ?? 0

  return (
    <Stack spacing={3} useFlexGap>
      <Stack
        spacing={3}
        sx={{
          position: 'relative',
          isolation: 'isolate',
          mt: { xs: -3, sm: -5 },
          pt: { xs: 3, sm: 5 },
          pb: 3,
          '&::before': {
            content: '""',
            position: 'absolute',
            zIndex: -1,
            top: 'calc(-1 * var(--customer-header-height, 77px))',
            bottom: 0,
            left: 'calc(50% - 50vw)',
            width: '100vw',
            backgroundColor: '#062b3c',
            backgroundImage: `url("${bannerImage}")`,
            // Beskär bort mer himmel och lyft fram landskapet längst ned.
            backgroundSize: 'cover',
            backgroundPosition: 'center bottom',
            backgroundRepeat: 'no-repeat',
          },
        }}
      >
        <EventCarousel events={featuredEvents} venuesById={venuesById} now={now} />

        <TextField
          label="Sök evenemang"
          value={searchTerm}
          onChange={(event) => setSearchTerm(event.target.value)}
          fullWidth
          sx={{ bgcolor: 'background.paper', borderRadius: 1 }}
        />
      </Stack>

      {eventsQuery.isPending ? (
        <Stack spacing={2} role="status" aria-busy="true" aria-label="Hämtar evenemang">
          {[1, 2, 3].map((item) => <Skeleton key={item} variant="rounded" height={150} />)}
        </Stack>
      ) : eventsQuery.isError && !eventsQuery.data ? (
        <Alert
          severity="error"
          action={<Button color="inherit" size="small" onClick={() => void eventsQuery.refetch()}>Försök igen</Button>}
        >
          Evenemangen kunde inte hämtas just nu. Kontrollera anslutningen och försök igen.
        </Alert>
      ) : visibleEvents.length === 0 ? (
        <Alert severity="info">Inga evenemang hittades.</Alert>
      ) : (
        <>
          <Typography variant="body2" color="text.secondary" role="status">
            Visar {visibleEvents.length} av {totalCount} evenemang
          </Typography>
          <Stack spacing={2}>
            {visibleEvents.map((event) => (
              <EventCard key={event.eventId} event={event} venueName={venuesById.get(event.venueId)} now={now} />
            ))}
          </Stack>
          {eventsQuery.isFetchNextPageError && (
            <Alert
              severity="error"
              action={<Button color="inherit" size="small" onClick={() => void eventsQuery.fetchNextPage()}>Försök igen</Button>}
            >
              Fler evenemang kunde inte hämtas.
            </Alert>
          )}
          {eventsQuery.hasNextPage && (
            <Stack ref={loadMoreRef} spacing={1} sx={{ alignItems: 'center' }} aria-live="polite">
              <Button
                variant="outlined"
                onClick={() => void eventsQuery.fetchNextPage()}
                disabled={eventsQuery.isFetchingNextPage || eventsQuery.isFetchNextPageError}
              >
                {eventsQuery.isFetchingNextPage ? 'Hämtar fler evenemang…' : `Visa fler (${eventPageSize})`}
              </Button>
            </Stack>
          )}
        </>
      )}
    </Stack>
  )
}
